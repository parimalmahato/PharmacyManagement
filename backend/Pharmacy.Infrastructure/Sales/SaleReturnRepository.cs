using System.Data;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Auditing;
using Pharmacy.Application.Payments.DTOs;
using Pharmacy.Application.Returns;
using Pharmacy.Domain.Entities;
using Pharmacy.Domain.Entities.Sales;
using Pharmacy.Domain.Entities.Sales.Returns;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Sales;

/// <summary>
/// Processes returns and actual refund records atomically with stock movements and auditing, enforcing original-line and settlement limits.
/// </summary>
public sealed class SaleReturnRepository(PharmacyDbContext db, IAuditService audit, IReturnInspectionContext? inspectionContext = null) : ISaleReturnRepository
{
    /// <summary>
    /// Validates remaining original-line quantities and disposition, then commits return credits, approved opt-in restocking and an optional confirmed payout together.
    /// </summary>
    public async Task<ReturnDto> CreateAsync(int saleId, CreateReturnRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = new SaleReturn(saleId, request.Reason, request.IdempotencyKey);
        if (request.Items is null || request.Items.Count == 0 || request.Items.Any(x => x is null || x.SaleItemId <= 0 || x.Quantity <= 0) ||
            request.Items.Select(x => x.SaleItemId).Distinct().Count() != request.Items.Count)
            throw new ArgumentException("Select unique sale items with positive return quantities.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var sale = await LockSale(saleId, cancellationToken);
        var existing = await db.SaleReturns.AsNoTracking().Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.SaleId == saleId && x.IdempotencyKey == request.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.Reason != result.Reason || existing.Items.Count != request.Items.Count ||
                request.Items.Any(item => !existing.Items.Any(x => x.SaleItemId == item.SaleItemId && x.Quantity == item.Quantity && x.Restock == item.Restock && x.Disposition == item.Disposition && x.InspectionApproved == item.InspectionApproved)))
                throw new InvalidOperationException("Idempotency key has already been used for a different return.");
            var existingRefund = await db.SaleRefunds.AsNoTracking().SingleOrDefaultAsync(x => x.SaleReturnId == existing.Id, cancellationToken);
            if ((request.Refund is null) != (existingRefund is null) || (request.Refund is not null && existingRefund is not null &&
                (request.Refund.Method != existingRefund.Method || request.Refund.Amount != existingRefund.Amount ||
                 (string.IsNullOrWhiteSpace(request.Refund.Reference) ? null : request.Refund.Reference.Trim()) != existingRefund.Reference)))
                throw new InvalidOperationException("Idempotency key has already been used with different refund details.");
            return Map(existing) with { Refund = existingRefund is null ? null : Map(existingRefund) };
        }
        if (sale.Status != SaleStatus.Completed) throw new InvalidOperationException("Only completed sales can be returned.");

        var items = await db.SaleItems.AsNoTracking().Where(x => x.SaleId == saleId).ToDictionaryAsync(x => x.Id, cancellationToken);
        var returned = await db.SaleReturnItems.Where(x => db.SaleReturns.Any(r => r.Id == x.SaleReturnId && r.SaleId == saleId))
            .GroupBy(x => x.SaleItemId).Select(g => new { Id = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.Id, x => x.Quantity, cancellationToken);

        // Lock stock in a stable order, matching checkout's UPDLOCK protocol.
        var restockIds = request.Items.Where(x => x.Restock && items.ContainsKey(x.SaleItemId))
            .Select(x => items[x.SaleItemId].MedicineBatchId).Distinct().OrderBy(x => x);
        var batches = new Dictionary<int, MedicineBatch>();
        foreach (var batchId in restockIds)
        {
            var batch = await db.MedicineBatches.FromSqlInterpolated($"SELECT * FROM dbo.MedicineBatches WITH (UPDLOCK, HOLDLOCK) WHERE Id = {batchId}")
                .SingleAsync(cancellationToken);
            if (batch.ExpiryDate <= Pharmacy.Domain.BusinessClock.Today)
                throw new InvalidOperationException("Expired returned medicines cannot be restocked. Record them without restocking.");
            batches.Add(batchId, batch);
        }
        foreach (var item in request.Items)
        {
            if (!items.TryGetValue(item.SaleItemId, out var original))
                throw new ArgumentException("A return item does not belong to this sale.");
            var amount = SaleSettlement.ReturnCredit(original.LineTotal, original.Quantity, returned.GetValueOrDefault(original.Id), item.Quantity);
            result.Items.Add(new SaleReturnItem(item.SaleItemId, item.Quantity, item.Restock, amount, item.Disposition,
                item.InspectionApproved, inspectionContext?.ApproverId));
            if (item.Restock)
            {
                var batch = batches[original.MedicineBatchId];
                if (item.Quantity > int.MaxValue - batch.Quantity) throw new InvalidOperationException("Restocking would exceed the batch quantity limit.");
                batch.AddStock(item.Quantity);
            }
        }
        db.SaleReturns.Add(result);
        await db.SaveChangesAsync(cancellationToken);
        foreach (var item in result.Items.Where(x => x.Restock))
            db.StockMovements.Add(new StockMovement(items[item.SaleItemId].MedicineBatchId, StockMovementType.SalesReturn,
                item.Quantity, $"SaleReturn:{result.Id}", result.Reason));
        await audit.WriteAsync("SaleReturned", "SaleReturn", result.Id.ToString(), newValues: Map(result),
            saveImmediately: false, cancellationToken: cancellationToken);
        SaleRefund? refund = null;
        if (request.Refund is not null)
        {
            // Return credit is visible inside this transaction. Refund validation,
            // stock, movements and both audit entries roll back together on failure.
            refund = await AddRefundAsync(sale, new RefundRequest(request.Refund.Method, request.Refund.Amount,
                request.Refund.Reference, request.IdempotencyKey), result.Id, cancellationToken);
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(result) with { Refund = refund is null ? null : Map(refund) };
    }

    /// <summary>
    /// Records an actual payout only within the remaining refund liability, using retry protection and audit history rather than initiating a transfer.
    /// </summary>
    public async Task<RefundDto> RefundAsync(int saleId, RefundRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var refund = new SaleRefund(saleId, request.Method, request.Amount, request.Reference, request.IdempotencyKey);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var sale = await LockSale(saleId, cancellationToken);
        var existing = await db.SaleRefunds.AsNoTracking().SingleOrDefaultAsync(x => x.SaleId == saleId && x.IdempotencyKey == request.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.SaleReturnId is not null || existing.Method != refund.Method || existing.Amount != refund.Amount || existing.Reference != refund.Reference)
                throw new InvalidOperationException("Idempotency key has already been used for a different refund.");
            return Map(existing);
        }
        refund = await AddRefundAsync(sale, request, null, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(refund);
    }

    private async Task<SaleRefund> AddRefundAsync(Sale sale, RefundRequest request, long? returnId, CancellationToken cancellationToken)
    {
        if (sale.Status is not (SaleStatus.Completed or SaleStatus.Cancelled)) throw new InvalidOperationException("This sale cannot be refunded.");
        var refund = new SaleRefund(sale.Id, request.Method, request.Amount, request.Reference, request.IdempotencyKey, returnId);
        if (await db.SaleRefunds.AnyAsync(x => x.SaleId == sale.Id && x.IdempotencyKey == request.IdempotencyKey, cancellationToken))
            throw new InvalidOperationException("Refund idempotency key is already in use.");
        var settlement = await GetSettlementAsync(db, sale.Id, sale.TotalAmount, cancellationToken);
        if (refund.Amount > settlement.RefundDue) throw new InvalidOperationException("Refund exceeds the amount due to the customer.");
        db.SaleRefunds.Add(refund);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("SaleRefunded", "SaleRefund", refund.Id.ToString(), newValues: Map(refund),
            saveImmediately: false, cancellationToken: cancellationToken);
        return refund;
    }

    /// <summary>
    /// Returns original returnable lines, recorded return/payout history and current settlement for a branch-visible sale.
    /// </summary>
    public async Task<SaleReturnsDto?> GetAsync(int saleId, CancellationToken cancellationToken = default)
    {
        if (saleId <= 0) throw new ArgumentException("Invalid sale ID.");
        var sale = await db.Sales.AsNoTracking().Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == saleId, cancellationToken);
        if (sale is null) return null;
        var returns = await db.SaleReturns.AsNoTracking().Include(x => x.Items).Where(x => x.SaleId == saleId).OrderBy(x => x.Id).ToListAsync(cancellationToken);
        var refunds = await db.SaleRefunds.AsNoTracking().Where(x => x.SaleId == saleId).OrderBy(x => x.Id).ToListAsync(cancellationToken);
        var batchIds = sale.Items.Select(x => x.MedicineBatchId).ToList();
        var batches = await db.MedicineBatches.AsNoTracking().Where(x => batchIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var paid = await db.Payments.Where(x => x.SaleId == saleId).SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var settlement = new SaleSettlement(sale.Status == SaleStatus.Cancelled ? 0m : sale.TotalAmount, paid, returns.Sum(x => x.TotalAmount), refunds.Sum(x => x.Amount));
        var available = sale.Items.OrderBy(x => x.Id).Select(item =>
        {
            var quantity = returns.SelectMany(x => x.Items).Where(x => x.SaleItemId == item.Id).Sum(x => x.Quantity);
            var batch = batches[item.MedicineBatchId];
            return new ReturnableItemDto(item.Id, item.MedicineNameSnapshot, batch.BatchNumber, batch.ExpiryDate,
                item.Quantity, quantity, sale.Status == SaleStatus.Cancelled ? 0 : item.Quantity - quantity, item.LineTotal, sale.Status == SaleStatus.Completed && batch.ExpiryDate > Pharmacy.Domain.BusinessClock.Today);
        }).ToList();
        return new(saleId, available, returns.Select(value => Map(value) with { Refund = refunds.Where(x => x.SaleReturnId == value.Id).Select(Map).SingleOrDefault() }).ToList(), refunds.Select(Map).ToList(),
            settlement.ReturnedAmount, settlement.RefundedAmount, settlement.RefundDue);
    }

    private async Task<Sale> LockSale(int saleId, CancellationToken cancellationToken) =>
        await db.Sales.FromSqlInterpolated($"SELECT * FROM dbo.Sales WITH (UPDLOCK, HOLDLOCK) WHERE Id = {saleId}")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken) ?? throw new KeyNotFoundException("Sale not found.");

    /// <summary>
    /// Loads recorded return credits, receipts and payouts to derive the sale&apos;s current balance and refund liability.
    /// </summary>
    internal static async Task<SaleSettlement> GetSettlementAsync(PharmacyDbContext db, int saleId, decimal total, CancellationToken cancellationToken)
    {
        if (await db.Sales.AnyAsync(x => x.Id == saleId && x.Status == SaleStatus.Cancelled, cancellationToken)) total = 0m;
        var paid = await db.Payments.Where(x => x.SaleId == saleId).SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var returned = await db.SaleReturnItems.Where(x => db.SaleReturns.Any(r => r.Id == x.SaleReturnId && r.SaleId == saleId))
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var refunded = await db.SaleRefunds.Where(x => x.SaleId == saleId).SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        return new(total, paid, returned, refunded);
    }
    private static ReturnDto Map(SaleReturn value) => new(value.Id, value.SaleId, value.Reason, value.IdempotencyKey,
        value.CreatedAtUtc, value.TotalAmount, value.Items.Select(x => new ReturnItemDto(x.SaleItemId, x.Quantity, x.Restock, x.Amount, x.Disposition.ToString(),
            x.InspectionApproved, x.InspectedByUserId, x.InspectedAtUtc)).ToList());
    private static RefundDto Map(SaleRefund value) => new(value.Id, value.SaleId, value.SaleReturnId, value.Method.ToString(), value.Amount,
        value.Reference, value.IdempotencyKey, value.CreatedAtUtc);
}

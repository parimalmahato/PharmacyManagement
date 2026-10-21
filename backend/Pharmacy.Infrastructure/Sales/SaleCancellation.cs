using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Sales;
using Pharmacy.Domain;
using Pharmacy.Domain.Entities;
using Pharmacy.Domain.Entities.Sales;

namespace Pharmacy.Infrastructure.Sales;

/// <summary>
/// Coordinates transactional checkout and cancellation with original-batch stock changes, invoice snapshots and retry protection.
/// </summary>
public sealed partial class SaleRepository
{
    /// <summary>
    /// Validates custody, existing adjustments and optional inspection before atomically preserving cancellation and any original-batch reversal; paid amounts become refund liabilities.
    /// </summary>
    public async Task<SaleDto> CancelAsync(int id, CancelSaleRequest request, string actorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (id <= 0 || request.IdempotencyKey == Guid.Empty || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 500)
            throw new ArgumentException("A valid sale, retry key and cancellation reason (up to 500 characters) are required.");
        if (!request.MedicinesNotDispensed) throw new ArgumentException("Cancellation requires confirmation that medicines never left pharmacy custody. Use returns for dispensed medicines.");
        if (request.Restock && !request.InspectionApproved) throw new ArgumentException("Explicit inspection approval is required before restoring sellable stock.");
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request with { Reason = request.Reason.Trim() }))));
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        // Same sale lock as receipts, returns and refunds: all settlement decisions serialize.
        var sale = await dbContext.Sales.FromSqlInterpolated($"SELECT * FROM dbo.Sales WITH (UPDLOCK, HOLDLOCK) WHERE Id = {id}")
            .SingleOrDefaultAsync(cancellationToken) ?? throw new KeyNotFoundException("Sale not found.");
        if (sale.Status == SaleStatus.Cancelled)
        {
            if (sale.CancellationKey != request.IdempotencyKey || sale.CancellationFingerprint != fingerprint)
                throw new InvalidOperationException("This sale has already been cancelled. Refresh its invoice.");
            return (await GetByIdAsync(id, cancellationToken))!;
        }
        if (await dbContext.SaleReturns.AnyAsync(x => x.SaleId == id, cancellationToken) || await dbContext.SaleRefunds.AnyAsync(x => x.SaleId == id, cancellationToken))
            throw new InvalidOperationException("A sale with existing returns or refunds cannot be cancelled. Continue the returns workflow.");
        if (request.Restock)
        {
            var quantities = await dbContext.SaleItems.Where(x => x.SaleId == id).GroupBy(x => x.MedicineBatchId)
                .Select(g => new { BatchId = g.Key, Quantity = g.Sum(x => (long)x.Quantity) }).OrderBy(x => x.BatchId).ToListAsync(cancellationToken);
            foreach (var item in quantities)
            {
                var batch = await dbContext.MedicineBatches.FromSqlInterpolated($"SELECT * FROM dbo.MedicineBatches WITH (UPDLOCK, HOLDLOCK) WHERE Id = {item.BatchId}")
                    .SingleOrDefaultAsync(cancellationToken) ?? throw new InvalidOperationException("Original batch is unavailable in this branch.");
                if (batch.ExpiryDate <= BusinessClock.Today) throw new InvalidOperationException("Expired medicines cannot re-enter sellable stock. Cancel without restocking and segregate the medicines.");
                if (item.Quantity > int.MaxValue - batch.Quantity) throw new InvalidOperationException("Restocking would exceed the batch quantity limit.");
                batch.AddStock((int)item.Quantity);
                dbContext.StockMovements.Add(new StockMovement(batch.Id, StockMovementType.SaleCancellation, (int)item.Quantity, $"SaleCancellation:{id}", request.Reason));
            }
        }
        sale.Cancel(request.Reason, actorId, request.IdempotencyKey, fingerprint, request.Restock);
        await auditService.WriteAsync("SaleCancelled", "Sale", id.ToString(), newValues: new {
            sale.CancellationReason, sale.CancelledByUserId, sale.CancelledAtUtc, sale.CancellationStockRestored,
            MedicinesNotDispensed = true, request.InspectionApproved,
            StockDisposition = request.Restock ? "Inspected saleable" : "Excluded from sellable stock; segregate physically"
        }, saveImmediately: false, cancellationToken: cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await GetByIdAsync(id, cancellationToken))!;
    }
}

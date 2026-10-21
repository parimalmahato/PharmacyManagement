using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Auditing;
using Pharmacy.Application.Payments;
using Pharmacy.Application.Payments.DTOs;
using Pharmacy.Domain.Entities.Payments;
using Pharmacy.Domain.Entities.Sales;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Payments;

/// <summary>
/// Records validated receipts with retry protection and audit history while preventing payments beyond the invoice balance.
/// </summary>
public sealed class PaymentRepository(PharmacyDbContext dbContext, IAuditService auditService) : IPaymentRepository
{
    /// <summary>
    /// Validates a confirmed receipt against the current balance and records it with idempotency and audit protection in a transaction.
    /// </summary>
    public async Task<PaymentDto> RecordAsync(int saleId, RecordPaymentRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var payment = new Payment(saleId, request.Method, request.Amount, request.Reference, request.IdempotencyKey);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        // Serialize all receipts for a sale before reading its balance. This also
        // makes concurrent identical retries wait for the original receipt.
        var sale = await dbContext.Sales
            .FromSqlInterpolated($"SELECT * FROM dbo.Sales WITH (UPDLOCK, HOLDLOCK) WHERE Id = {saleId}")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Sale not found.");

        var existing = await dbContext.Payments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.IdempotencyKey == payment.IdempotencyKey, cancellationToken);
        if (existing is not null)
            return MatchRetry(existing, payment);

        if (sale.Status != SaleStatus.Completed)
            throw new InvalidOperationException("Payments can only be recorded for completed sales.");

        var settlement = await Pharmacy.Infrastructure.Sales.SaleReturnRepository.GetSettlementAsync(dbContext, saleId, sale.TotalAmount, cancellationToken);
        if (payment.Amount > settlement.BalanceAmount)
            throw new InvalidOperationException("Payment exceeds the outstanding sale balance.");

        dbContext.Payments.Add(payment);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditService.WriteAsync("PaymentRecorded", "Payment", payment.Id.ToString(),
                newValues: Map(payment), saveImmediately: false, cancellationToken: cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Map(payment);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // A globally unique key may race across different sales.
            await transaction.RollbackAsync(cancellationToken);
            dbContext.Entry(payment).State = EntityState.Detached;
            var winner = await dbContext.Payments.AsNoTracking()
                .SingleOrDefaultAsync(x => x.IdempotencyKey == payment.IdempotencyKey, cancellationToken);
            if (winner is null) throw;
            return MatchRetry(winner, payment);
        }
    }

    /// <summary>
    /// Returns receipt history and current settlement for a branch-visible invoice or null when absent.
    /// </summary>
    public async Task<SalePaymentsDto?> GetBySaleAsync(int saleId, CancellationToken cancellationToken = default)
    {
        if (saleId <= 0) throw new ArgumentException("Invalid sale ID.");
        var sale = await dbContext.Sales.AsNoTracking()
            .Where(x => x.Id == saleId).Select(x => new { x.Id, x.TotalAmount, x.Status })
            .SingleOrDefaultAsync(cancellationToken);
        if (sale is null) return null;
        var payments = await dbContext.Payments.AsNoTracking().Where(x => x.SaleId == saleId)
            .OrderBy(x => x.Id).ToListAsync(cancellationToken);
        var settlement = await Pharmacy.Infrastructure.Sales.SaleReturnRepository.GetSettlementAsync(dbContext, saleId, sale.TotalAmount, cancellationToken);
        return new(sale.Id, sale.TotalAmount, settlement.PaidAmount, settlement.BalanceAmount, settlement.PaymentStatus, payments.Select(Map).ToList())
        { IsCancelled = sale.Status == SaleStatus.Cancelled, ReturnedAmount = settlement.ReturnedAmount, RefundedAmount = settlement.RefundedAmount, RefundDue = settlement.RefundDue };
    }

    private static PaymentDto MatchRetry(Payment existing, Payment requested)
    {
        if (existing.SaleId != requested.SaleId || existing.Method != requested.Method ||
            existing.Amount != requested.Amount || existing.Reference != requested.Reference)
            throw new InvalidOperationException("Idempotency key has already been used for a different payment.");
        return Map(existing);
    }

    private static PaymentDto Map(Payment payment) => new(payment.Id, payment.SaleId,
        payment.Method.ToString(), payment.Amount, payment.Reference, payment.IdempotencyKey, payment.CreatedAtUtc);
}

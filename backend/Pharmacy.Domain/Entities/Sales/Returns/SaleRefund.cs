using Pharmacy.Domain.Entities.Payments;

namespace Pharmacy.Domain.Entities.Sales.Returns;

/// <summary>
/// Supported channels for recording an actual refund payout.
/// </summary>
public enum RefundMethod { Cash = 1, Upi = 2, Card = 3 }

/// <summary>
/// Records a confirmed payout against a sale, optionally linked to its return; it does not initiate a bank transaction.
/// </summary>
public sealed class SaleRefund
{
    private SaleRefund() { }
    public long Id { get; private set; }
    public int SaleId { get; private set; }
    public RefundMethod Method { get; private set; }
    public long? SaleReturnId { get; private set; }
    public decimal Amount { get; private set; }
    public string? Reference { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public SaleRefund(int saleId, RefundMethod method, decimal amount, string? reference, Guid idempotencyKey, long? saleReturnId = null)
    {
        // Refund receipts use the same monetary and reference rules as payments.
        if (!Enum.IsDefined(method)) throw new ArgumentException("Invalid refund method.");
        if (saleReturnId.HasValue && saleReturnId <= 0) throw new ArgumentException("Invalid return ID.");
        var validated = new Payment(saleId, (PaymentMethod)method, amount, reference, idempotencyKey);
        SaleReturnId = saleReturnId;
        SaleId = saleId; Method = method; Amount = amount; Reference = validated.Reference;
        IdempotencyKey = idempotencyKey; CreatedAtUtc = DateTime.UtcNow;
    }
}

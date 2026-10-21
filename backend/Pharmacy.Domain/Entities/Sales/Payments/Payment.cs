namespace Pharmacy.Domain.Entities.Payments;

/// <summary>
/// Supported receipt channels recorded after staff confirm actual payment.
/// </summary>
public enum PaymentMethod { Cash = 1, Upi = 2, Card = 3 }
/// <summary>
/// Describes whether recorded receipts cover the invoice amount.
/// </summary>
public enum PaymentStatus { Unpaid = 1, PartiallyPaid = 2, Paid = 3 }

/// <summary>
/// An immutable record of a confirmed customer receipt with its method, reference and retry identity; creating it does not initiate a transfer.
/// </summary>
public sealed class Payment
{
    private Payment() { }
    public long Id { get; private set; }
    public int SaleId { get; private set; }
    public PaymentMethod Method { get; private set; }
    public decimal Amount { get; private set; }
    public string? Reference { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public Payment(int saleId, PaymentMethod method, decimal amount, string? reference, Guid idempotencyKey)
    {
        if (saleId <= 0) throw new ArgumentException("Invalid sale ID.");
        if (!Enum.IsDefined(method)) throw new ArgumentException("Invalid payment method.");
        if (amount <= 0 || amount > 9999999999999999.99m || decimal.Round(amount, 2) != amount) throw new ArgumentException("Amount must fit decimal(18,2), be positive and have at most two decimal places.");
        if (idempotencyKey == Guid.Empty) throw new ArgumentException("Idempotency key is required.");
        if (method != PaymentMethod.Cash && string.IsNullOrWhiteSpace(reference)) throw new ArgumentException("UPI/card reference is required.");
        if (reference?.Trim().Length > 150) throw new ArgumentException("Reference is too long.");
        SaleId = saleId;
        Method = method;
        Amount = amount;
        Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        IdempotencyKey = idempotencyKey;
        CreatedAtUtc = DateTime.UtcNow;
    }
}

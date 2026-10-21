
namespace Pharmacy.Domain.Entities.Sales;

/// <summary>
/// An issued invoice aggregate whose original item and tax snapshots are preserved when later payments, returns or cancellation occur.
/// </summary>
public class Sale
{
    private readonly List<SaleItem> _items = [];
    public ICollection<Returns.SaleReturn> Returns { get; private set; } = new List<Returns.SaleReturn>();
    public ICollection<Returns.SaleRefund> Refunds { get; private set; } = new List<Returns.SaleRefund>();
    public ICollection<Pharmacy.Domain.Entities.Payments.Payment> Payments { get; private set; }
        = new List<Pharmacy.Domain.Entities.Payments.Payment>();

    private Sale() { } // EF Core

    public int Id { get; private set; }
    public Guid? CheckoutKey { get; private set; }
    public string? CheckoutFingerprint { get; private set; }
    /// <summary>
    /// Attaches a non-empty retry identity and request fingerprint to an unsaved sale so repeated checkout attempts can resolve to the same operation.
    /// </summary>
    public void IdentifyCheckout(Guid key, string fingerprint)
    {
        if (Id != 0 || key == Guid.Empty || fingerprint.Length != 64) throw new ArgumentException("Invalid checkout identity.");
        CheckoutKey = key; CheckoutFingerprint = fingerprint;
    }

    public string InvoiceNumber { get; private set; }
        = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public decimal SubtotalAmount { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal TaxableAmount { get; private set; }
    public decimal GstAmount { get; private set; }
    /// <summary>
    /// Original issued invoice amount; later returns and cancellation are represented separately rather than rewriting this total.
    /// </summary>
    public decimal TotalAmount { get; private set; }

    public SaleStatus Status { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }
    public string? CancelledByUserId { get; private set; }
    public string? CancellationReason { get; private set; }
    public Guid? CancellationKey { get; private set; }
    public string? CancellationFingerprint { get; private set; }
    /// <summary>
    /// Whether cancellation posted an approved reversal into the original batches; cancellation alone does not imply restocking.
    /// </summary>
    public bool CancellationStockRestored { get; private set; }

    /// <summary>
    /// Marks a completed invoice as cancelled and records actor, reason, retry identity and stock handling; the repository validates custody and commits related changes.
    /// </summary>
    public void Cancel(string reason, string actorId, Guid key, string fingerprint, bool stockRestored)
    {
        if (Status != SaleStatus.Completed) throw new InvalidOperationException("Only completed sales can be cancelled.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500) throw new ArgumentException("A cancellation reason of up to 500 characters is required.");
        if (string.IsNullOrWhiteSpace(actorId) || actorId.Length > 450 || key == Guid.Empty || fingerprint.Length != 64) throw new ArgumentException("Invalid cancellation identity.");
        Status = SaleStatus.Cancelled;
        CancelledAtUtc = DateTime.UtcNow; CancelledByUserId = actorId;
        CancellationReason = reason.Trim(); CancellationKey = key;
        CancellationFingerprint = fingerprint; CancellationStockRestored = stockRestored;
    }

    public int? CustomerId { get; private set; }

    public Customer? Customer { get; private set; }

    public IReadOnlyCollection<SaleItem> Items =>
        _items.AsReadOnly();

    public Sale(string invoiceNumber)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new ArgumentException(
                "Invoice number is required.");

        InvoiceNumber = invoiceNumber;
        CreatedAtUtc = DateTime.UtcNow;
        Status = SaleStatus.Completed;
    }

    /// <summary>
    /// Adds an invoice-time sale-line snapshot to a completed aggregate and recomputes its original monetary totals.
    /// </summary>
    public void AddItem(SaleItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (Status != SaleStatus.Completed)
            throw new InvalidOperationException(
                "Items cannot be added to this sale.");

        _items.Add(item);

        SubtotalAmount = _items.Sum(x => x.GrossAmount);
        DiscountAmount = _items.Sum(x => x.DiscountAmount);
        TaxableAmount = _items.Sum(x => x.TaxableAmount);
        GstAmount = _items.Sum(x => x.GstAmount);
        TotalAmount = _items.Sum(x => x.LineTotal);
    }

    /// <summary>
    /// Associates a valid customer before the sale is persisted; issued invoice ownership is not changed through this operation.
    /// </summary>
    public void AssignCustomer(int customerId)
    {
        if (customerId <= 0)
            throw new ArgumentException(
                "Customer ID must be greater than zero.",
                nameof(customerId));

        if (Id > 0)
            throw new InvalidOperationException(
                "Customer cannot be changed after saving.");

        CustomerId = customerId;
    }
}

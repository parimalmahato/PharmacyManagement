namespace Pharmacy.Domain.Entities.Sales.Returns;

/// <summary>
/// Describes the condition of returned medicines and separates inspected saleable goods from unsuitable or uninspected returns.
/// </summary>
public enum ReturnDisposition { Saleable = 1, Damaged = 2, Expired = 3, Quarantine = 4, NonResaleable = 5 }

/// <summary>
/// Preserves a return event linked to an original sale, including reason, retry identity and item credits.
/// </summary>
public sealed class SaleReturn
{
    private SaleReturn() { }
    public long Id { get; private set; }
    public int SaleId { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public List<SaleReturnItem> Items { get; private set; } = [];
    public decimal TotalAmount => Items.Sum(x => x.Amount);

    public SaleReturn(int saleId, string reason, Guid idempotencyKey)
    {
        if (saleId <= 0) throw new ArgumentException("Invalid sale ID.");
        if (idempotencyKey == Guid.Empty) throw new ArgumentException("Idempotency key is required.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
            throw new ArgumentException("A return reason of at most 500 characters is required.");
        SaleId = saleId; Reason = reason.Trim(); IdempotencyKey = idempotencyKey; CreatedAtUtc = DateTime.UtcNow;
    }
}

/// <summary>
/// Links returned quantity and credit to an original sale line and records disposition, restocking and inspection approval.
/// </summary>
public sealed class SaleReturnItem
{
    private SaleReturnItem() { }
    public long Id { get; private set; }
    public long SaleReturnId { get; private set; }
    public int SaleItemId { get; private set; }
    public int Quantity { get; private set; }
    /// <summary>
    /// Records whether this return actually restored stock to its original batch after eligible inspection approval.
    /// </summary>
    public bool Restock { get; private set; }
    public ReturnDisposition Disposition { get; private set; }
    /// <summary>
    /// Explicit resale-inspection approval recorded with the approving operator when required for saleable returns.
    /// </summary>
    public bool InspectionApproved { get; private set; }
    public string? InspectedByUserId { get; private set; }
    public DateTime? InspectedAtUtc { get; private set; }
    public decimal Amount { get; private set; }

    public SaleReturnItem(int saleItemId, int quantity, bool restock, decimal amount, ReturnDisposition disposition = ReturnDisposition.Quarantine,
        bool inspectionApproved = false, string? inspectedByUserId = null)
    {
        if (saleItemId <= 0 || quantity <= 0) throw new ArgumentException("A valid sale item and positive quantity are required.");
        if (amount < 0 || decimal.Round(amount, 2) != amount) throw new ArgumentException("Invalid return credit.");
        if (!Enum.IsDefined(disposition)) throw new ArgumentException("Invalid return disposition.");
        if (restock && disposition != ReturnDisposition.Saleable)
            throw new ArgumentException("Only inspected saleable returns may be restocked.");
        if (inspectionApproved && (string.IsNullOrWhiteSpace(inspectedByUserId) || inspectedByUserId.Length > 450))
            throw new ArgumentException("An authenticated inspection approver is required.");
        if (restock && !inspectionApproved)
            throw new ArgumentException("Inspection approval is required before restocking.");
        InspectionApproved = inspectionApproved;
        InspectedByUserId = inspectionApproved ? inspectedByUserId : null;
        InspectedAtUtc = inspectionApproved ? DateTime.UtcNow : null;
        Disposition = disposition;
        SaleItemId = saleItemId; Quantity = quantity; Restock = restock; Amount = amount;
    }
}

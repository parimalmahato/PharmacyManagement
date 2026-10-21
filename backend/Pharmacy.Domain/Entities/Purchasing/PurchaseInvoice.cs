namespace Pharmacy.Domain.Entities.Purchasing;

/// <summary>
/// A supplier invoice aggregate whose draft can be reviewed before receiving its items into branch stock.
/// </summary>
public sealed class PurchaseInvoice
{
    private readonly List<PurchaseItem> _items = [];

    public int Id { get; private set; }
    public int SupplierId { get; private set; }
    public string InvoiceNumber { get; private set; } = string.Empty;
    public DateOnly InvoiceDate { get; private set; }
    public PurchaseStatus Status { get; private set; } = PurchaseStatus.Draft;
    public decimal TotalAmount { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public Supplier Supplier { get; private set; } = null!;
    public IReadOnlyCollection<PurchaseItem> Items => _items.AsReadOnly();
    public byte[] RowVersion { get; private set; } = [];

    private PurchaseInvoice() { } // EF Core

    public PurchaseInvoice(
        int supplierId,
        string invoiceNumber,
        DateOnly invoiceDate)
    {
        if (supplierId <= 0)
            throw new ArgumentException("A valid supplier is required.");

        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new ArgumentException("Invoice number is required.");

        if (invoiceNumber.Trim().Length > 100)
            throw new ArgumentException("Invoice number cannot exceed 100 characters.");

        SupplierId = supplierId;
        InvoiceNumber = invoiceNumber.Trim();
        InvoiceDate = invoiceDate;
        Status = PurchaseStatus.Draft;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Adds a line only while the supplier invoice remains a draft and recalculates its recorded purchase total.
    /// </summary>
    public void AddItem(PurchaseItem item)
    {
        if (Status != PurchaseStatus.Draft)
            throw new InvalidOperationException(
                "Only draft purchases can be modified.");

        _items.Add(item);
        RecalculateTotal();
    }

    /// <summary>
    /// Moves a non-empty draft to received state; the receiving repository coordinates actual batch and stock-movement writes in the same transaction.
    /// </summary>
    public void MarkReceived()
    {
        if (Status != PurchaseStatus.Draft)
            throw new InvalidOperationException(
                "Only draft purchases can be received.");

        if (_items.Count == 0)
            throw new InvalidOperationException(
                "Add at least one item before receiving the purchase.");

        Status = PurchaseStatus.Received;
    }

    /// <summary>
    /// Cancels only an unreceived draft; this operation does not reverse stock from an already received supplier invoice.
    /// </summary>
    public void Cancel()
    {
        if (Status != PurchaseStatus.Draft)
            throw new InvalidOperationException(
                "Only draft purchases can be cancelled.");

        Status = PurchaseStatus.Cancelled;
    }

    private void RecalculateTotal()
    {
        TotalAmount = _items.Sum(
            item => item.Quantity * item.PurchasePrice);
    }
}
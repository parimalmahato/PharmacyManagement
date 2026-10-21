namespace Pharmacy.Domain.Entities.Purchasing;

/// <summary>
/// A supplier-invoice line describing a medicine lot, future expiry, inventory-unit quantity and unit prices.
/// </summary>
public sealed class PurchaseItem
{
    public int Id { get; private set; }
    public int PurchaseInvoiceId { get; private set; }
    public int MedicineId { get; private set; }

    public string BatchNumber { get; private set; } = string.Empty;
    public DateOnly ExpiryDate { get; private set; }
    public int Quantity { get; private set; }
    public decimal PurchasePrice { get; private set; }
    public decimal SellingPrice { get; private set; }

    public PurchaseInvoice PurchaseInvoice { get; private set; } = null!;

    private PurchaseItem() { } // EF Core

    public PurchaseItem(
        int medicineId,
        string batchNumber,
        DateOnly expiryDate,
        int quantity,
        decimal purchasePrice,
        decimal sellingPrice)
    {
        if (medicineId <= 0)
            throw new ArgumentException("A valid medicine is required.");

        if (string.IsNullOrWhiteSpace(batchNumber))
            throw new ArgumentException("Batch number is required.");

        if (batchNumber.Trim().Length > 50)
            throw new ArgumentException("Batch number cannot exceed 50 characters.");

        if (expiryDate <= Pharmacy.Domain.BusinessClock.Today)
            throw new ArgumentException("The batch must not be expired.");

        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero.");

        if (purchasePrice < 0 || sellingPrice < 0)
            throw new ArgumentException("Prices cannot be negative.");

        if (sellingPrice < purchasePrice)
            throw new ArgumentException(
                "Selling price cannot be lower than purchase price.");

        MedicineId = medicineId;
        BatchNumber = batchNumber.Trim();
        ExpiryDate = expiryDate;
        Quantity = quantity;
        PurchasePrice = purchasePrice;
        SellingPrice = sellingPrice;
    }
}
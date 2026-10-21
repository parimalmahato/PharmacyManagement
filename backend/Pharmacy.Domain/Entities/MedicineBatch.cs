
namespace Pharmacy.Domain.Entities;

/// <summary>
/// A branch-owned medicine lot with expiry, unit costs and quantity; selling rejects batches expiring on or before the business date.
/// </summary>
public class MedicineBatch
{
    public int Id { get; private set; }

    public int MedicineId { get; private set; }

    public string BatchNumber { get; private set; } = string.Empty;

    /// <summary>
    /// Business expiry date; selling is rejected when this date is on or before the current business date.
    /// </summary>
    public DateOnly ExpiryDate { get; private set; }

    /// <summary>
    /// Recorded purchase cost per inventory unit used by stock valuation and gross-profit calculations.
    /// </summary>
    public decimal PurchasePrice { get; private set; }

    /// <summary>
    /// GST-inclusive selling price per inventory unit used when checkout captures the sale-line snapshot.
    /// </summary>
    public decimal SellingPrice { get; private set; }

    /// <summary>
    /// Recorded inventory units for this lot; quantities alone do not establish that the lot is unexpired or saleable.
    /// </summary>
    public int Quantity { get; private set; }

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    public Medicine Medicine { get; private set; } = null!;

    private MedicineBatch() { } // EF Core

    public MedicineBatch(
        int medicineId,
        string batchNumber,
        DateOnly expiryDate,
        decimal purchasePrice,
        decimal sellingPrice,
        int quantity)
    {
        if (medicineId <= 0)
            throw new ArgumentException("A valid medicine is required.");

        if (string.IsNullOrWhiteSpace(batchNumber))
            throw new ArgumentException("Batch number is required.");

        if (batchNumber.Trim().Length > 50)
            throw new ArgumentException("Batch number cannot exceed 50 characters.");

        if (expiryDate <= Pharmacy.Domain.BusinessClock.Today)
            throw new ArgumentException("Expiry date must be in the future.");

        if (purchasePrice < 0)
            throw new ArgumentException("Purchase price cannot be negative.");

        if (sellingPrice < 0)
            throw new ArgumentException("Selling price cannot be negative.");

        if (quantity < 0)
            throw new ArgumentException("Quantity cannot be negative.");

        MedicineId = medicineId;
        BatchNumber = batchNumber.Trim();
        ExpiryDate = expiryDate;
        PurchasePrice = purchasePrice;
        SellingPrice = sellingPrice;
        Quantity = quantity;
    }

    /// <summary>
    /// Adds a strictly positive inventory-unit quantity with overflow checking; the calling workflow owns inspection and transactional stock-movement recording.
    /// </summary>
    public void AddStock(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Stock addition must be positive.");

        Quantity = checked(Quantity + quantity);
    }

    /// <summary>
    /// Rejects non-positive requests, expired lots and insufficient quantity before decreasing sellable stock.
    /// </summary>
    public void RemoveStock(int quantity)
    {
        if (quantity <= 0)
        throw new ArgumentException(
            "Quantity must be greater than zero.");

        if (ExpiryDate <= Pharmacy.Domain.BusinessClock.Today)
            throw new InvalidOperationException(
                "Expired stock cannot be sold.");

        if (Quantity < quantity)
            throw new InvalidOperationException(
                "Insufficient stock.");

        Quantity -= quantity;
    }
}

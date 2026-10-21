namespace Pharmacy.Domain.Entities;

/// <summary>
/// Identifies the operational reason for a recorded inventory quantity change.
/// </summary>
public enum StockMovementType
{
    PurchaseReceived = 1,
    Sale = 2,
    SupplierReturn = 3,
    Damaged = 4,
    StockAdjustment = 5,
    SalesReturn = 6,
    SaleCancellation = 7
}

/// <summary>
/// Records the batch, signed quantity change and source of a stock operation for inventory traceability.
/// </summary>
public sealed class StockMovement
{
    public long Id { get; private set; }
    public int MedicineBatchId { get; private set; }
    public StockMovementType MovementType { get; private set; }
    public int QuantityChange { get; private set; }
    public string Reference { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public MedicineBatch MedicineBatch { get; private set; } = null!;

    private StockMovement() { } // EF Core

    public StockMovement(
        int medicineBatchId,
        StockMovementType movementType,
        int quantityChange,
        string reference,
        string? notes = null)
    {
        if (medicineBatchId <= 0)
            throw new ArgumentException("A valid medicine batch is required.");

        if (quantityChange == 0)
            throw new ArgumentException("Quantity change cannot be zero.");

        if (string.IsNullOrWhiteSpace(reference))
            throw new ArgumentException("A reference is required.");

        if (reference.Trim().Length > 150)
            throw new ArgumentException("Reference cannot exceed 150 characters.");

        if (movementType is StockMovementType.PurchaseReceived or StockMovementType.SalesReturn or StockMovementType.SaleCancellation &&
            quantityChange <= 0)
        {
            throw new ArgumentException(
                "A purchase receipt must increase stock.");
        }

        if (movementType is StockMovementType.Sale
            or StockMovementType.SupplierReturn
            or StockMovementType.Damaged && quantityChange >= 0)
        {
            throw new ArgumentException(
                "This movement type must decrease stock.");
        }

        // if ((movementType is StockMovementType.Sale
        // or StockMovementType.SupplierReturn
        // or StockMovementType.Damaged)
        // && quantityChange >= 0)
        // {
        //     throw new ArgumentException(
        //         "This movement type must decrease stock.");
        // }

        MedicineBatchId = medicineBatchId;
        MovementType = movementType;
        QuantityChange = quantityChange;
        Reference = reference.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        CreatedAtUtc = DateTime.UtcNow;
    }
}

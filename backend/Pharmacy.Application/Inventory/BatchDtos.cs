
namespace Pharmacy.Application.Inventory;

/// <summary>
/// Opening-stock input for a medicine lot, including expiry, inventory-unit quantity and purchase/selling prices.
/// </summary>
public sealed record CreateBatchRequest(
    int MedicineId,
    string BatchNumber,
    DateOnly ExpiryDate,
    decimal PurchasePrice,
    decimal SellingPrice,
    int Quantity);

/// <summary>
/// Inventory display data for a branch batch, including price, quantity and calculated alert flags.
/// </summary>
public sealed record BatchDto(
    int Id,
    int MedicineId,
    string MedicineName,
    string BatchNumber,
    DateOnly ExpiryDate,
    decimal PurchasePrice,
    decimal SellingPrice,
    int Quantity,
    bool IsExpired,
    bool IsNearExpiry,
    bool IsLowStock);

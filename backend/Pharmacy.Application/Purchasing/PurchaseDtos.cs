namespace Pharmacy.Application.Purchasing;

/// <summary>
/// Shared supplier profile input used before creating branch-specific purchase invoices.
/// </summary>
public sealed record CreateSupplierRequest(
    string Name,
    string? Phone,
    string? Email,
    string? Address);

/// <summary>
/// Shared supplier identity, contact and activation information returned to purchasing screens.
/// </summary>
public sealed record SupplierDto(
    int Id,
    string Name,
    string? Phone,
    string? Email,
    string? Address,
    bool IsActive);

/// <summary>
/// A draft supplier-invoice line specifying a medicine lot and per-inventory-unit quantities and prices.
/// </summary>
public sealed record CreatePurchaseItemRequest(
    int MedicineId,
    string BatchNumber,
    DateOnly ExpiryDate,
    int Quantity,
    decimal PurchasePrice,
    decimal SellingPrice);

/// <summary>
/// A supplier-invoice draft request; stock changes only after the separate receiving workflow succeeds.
/// </summary>
public sealed record CreatePurchaseRequest(
    int SupplierId,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    List<CreatePurchaseItemRequest> Items);

/// <summary>
/// A supplier-invoice item&apos;s original batch, expiry, quantity and unit prices.
/// </summary>
public sealed record PurchaseItemDto(
    int MedicineId,
    string BatchNumber,
    DateOnly ExpiryDate,
    int Quantity,
    decimal PurchasePrice,
    decimal SellingPrice);

/// <summary>
/// Branch supplier-invoice header, items, status and recorded purchase total.
/// </summary>
public sealed record PurchaseDto(
    int Id,
    int SupplierId,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    string Status,
    decimal TotalAmount,
    List<PurchaseItemDto> Items);
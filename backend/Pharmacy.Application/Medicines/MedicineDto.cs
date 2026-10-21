namespace Pharmacy.Application.Medicines;

/// <summary>
/// Medicine-catalog data returned independently of branch batch stock quantities.
/// </summary>
public sealed record MedicineDto(
    int Id,
    string Name,
    string GenericName,
    string Manufacturer,
    string Category,
    bool IsActive,
    string? HsnCode,
    decimal GstPercentage);

/// <summary>
/// Descriptive and tax configuration for a shared catalog medicine; creating it does not add inventory.
/// </summary>
public sealed record CreateMedicineRequest(
    string Name,
    string GenericName,
    string Manufacturer,
    string Category,
    string? HsnCode = null,
    decimal? GstPercentage = null);

/// <summary>
/// Replacement catalog details that do not rewrite previously issued sale-line snapshots.
/// </summary>
public sealed record UpdateMedicineRequest(
    string Name,
    string GenericName,
    string Manufacturer,
    string Category,
    string? HsnCode = null,
    decimal GstPercentage = 0m);

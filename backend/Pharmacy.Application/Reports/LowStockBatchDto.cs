
namespace Pharmacy.Application.Reports;

/// <summary>
/// A batch-level inventory alert using the reporting threshold rather than a purchase order recommendation.
/// </summary>
public sealed record LowStockBatchDto(
    int BatchId,
    int MedicineId,
    string MedicineName,
    string BatchNumber,
    int Quantity,
    DateOnly ExpiryDate
);

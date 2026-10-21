
namespace Pharmacy.Application.Reports;

/// <summary>
/// Counts used to summarize branch batch-expiry alerts.
/// </summary>
public sealed record ExpirySummaryDto(
    int ExpiredBatches,
    int ExpiringWithin30Days,
    int ExpiringWithin60Days,
    int ExpiringWithin90Days
);

/// <summary>
/// Medicine and batch details returned for an expiry-management result.
/// </summary>
public sealed record ExpiringBatchDto(
    int BatchId,
    int MedicineId,
    string MedicineName,
    string BatchNumber,
    DateOnly ExpiryDate,
    int Quantity,
    int DaysUntilExpiry,
    string Status
);

/// <summary>
/// Branch expiry summary and batch lists used for staff review and segregation decisions.
/// </summary>
public sealed record ExpiryReportDto(
    ExpirySummaryDto Summary,
    List<ExpiringBatchDto> Batches
);

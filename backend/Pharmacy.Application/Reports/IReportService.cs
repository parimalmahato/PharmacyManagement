namespace Pharmacy.Application.Reports;

/// <summary>
/// Defines dashboard and report use cases using explicit business-date boundaries.
/// </summary>
public interface IReportService
{
    /// <summary>
    /// Returns the reporting repository&apos;s branch dashboard aggregates.
    /// </summary>
    Task<DashboardSummaryDto> GetDashboardSummaryAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the report period and returns branch sales, credits and cash activity for that interval.
    /// </summary>
    Task<SalesReportDto> GetSalesReportAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// Returns branch batch alerts for stock review.
    /// </summary>
    Task<List<LowStockBatchDto>> GetLowStockBatchesAsync(
        CancellationToken cancellationToken = default);
    /// <summary>
    /// Returns branch expiry-management data without disposing of or transferring batches.
    /// </summary>
    Task<ExpiryReportDto> GetExpiryReportAsync(
        CancellationToken cancellationToken = default);
}
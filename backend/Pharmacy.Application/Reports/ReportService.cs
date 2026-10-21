
namespace Pharmacy.Application.Reports;

/// <summary>
/// Validates report inputs and delegates branch analytics retrieval to the reporting repository.
/// </summary>
public sealed class ReportService : IReportService
{
    private readonly IReportRepository _reportRepository;

    public ReportService(IReportRepository reportRepository)
    {
        _reportRepository = reportRepository;
    }

    /// <summary>
    /// Returns the reporting repository&apos;s branch dashboard aggregates.
    /// </summary>
    public Task<DashboardSummaryDto> GetDashboardSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        return _reportRepository.GetDashboardSummaryAsync(
            cancellationToken);
    }

    /// <summary>
    /// Validates the report period and returns branch sales, credits and cash activity for that interval.
    /// </summary>
    public Task<SalesReportDto> GetSalesReportAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        _ = BusinessDates.Range(startDate, endDate);
        if (startDate > endDate)
        {
            throw new ArgumentException(
                "Start date cannot be later than end date.");
        }

        if (endDate.DayNumber - startDate.DayNumber > 365)
        {
            throw new ArgumentException(
                "The report date range cannot exceed 366 calendar days.");
        }

        return _reportRepository.GetSalesReportAsync(
            startDate,
            endDate,
            cancellationToken);
    }
    /// <summary>
    /// Returns branch batch alerts for stock review.
    /// </summary>
    public Task<List<LowStockBatchDto>> GetLowStockBatchesAsync(
         CancellationToken cancellationToken = default)
    {
        return _reportRepository.GetLowStockBatchesAsync(
            cancellationToken);
    }
    /// <summary>
    /// Returns branch expiry-management data without disposing of or transferring batches.
    /// </summary>
    public Task<ExpiryReportDto> GetExpiryReportAsync(
        CancellationToken cancellationToken = default)
    {
    return _reportRepository.GetExpiryReportAsync(
        cancellationToken);
    }
}

namespace Pharmacy.Application.Reports;

/// <summary>
/// Defines branch analytics queries that distinguish invoice activity, credits, receipts, refunds and stock alerts.
/// </summary>
public interface IReportRepository
{
    /// <summary>
    /// Aggregates current branch dashboard activity and liabilities, distinguishing invoice revenue from cash collections and refund payouts.
    /// </summary>
    Task<DashboardSummaryDto> GetDashboardSummaryAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds dated sales and cash activity using India business-date UTC boundaries; credits retain their own event dates.
    /// </summary>
    Task<SalesReportDto> GetSalesReportAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// Reads branch batch alerts using the configured low-stock threshold.
    /// </summary>
    Task<List<LowStockBatchDto>> GetLowStockBatchesAsync(
        CancellationToken cancellationToken = default);
    /// <summary>
    /// Builds branch expiry summaries and batch lists for staff review without automatically changing stock.
    /// </summary>
    Task<ExpiryReportDto> GetExpiryReportAsync(
        CancellationToken cancellationToken = default);
}
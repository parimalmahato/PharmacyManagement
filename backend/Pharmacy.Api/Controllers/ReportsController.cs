
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Application.Reports;
using Microsoft.AspNetCore.Authorization;
using Pharmacy.Application.Authorization;
namespace Pharmacy.Api.Controllers;
/// <summary>
/// Exposes authorized branch dashboards, sales reports and inventory alerts with validated report periods.
/// </summary>
[Authorize(Policy = ApplicationPolicies.ViewReports)]
[ApiController]
[Route("api/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>
    /// Returns branch dashboard aggregates for authorized reporting roles.
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardSummaryDto>> GetDashboard(
        CancellationToken cancellationToken)
    {
        var result = await _reportService.GetDashboardSummaryAsync(
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Validates the requested period and returns branch invoice, credit and cash activity.
    /// </summary>
    [HttpGet("sales")]
    public async Task<ActionResult<SalesReportDto>> GetSalesReport(
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken cancellationToken)
    {
        try { _ = BusinessDates.Range(startDate, endDate); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        if (startDate > endDate)
        {
            return BadRequest(new
            {
                message = "Start date cannot be later than end date."
            });
        }

        if (endDate.DayNumber - startDate.DayNumber > 365)
        {
            return BadRequest(new
            {
                message = "The report date range cannot exceed 366 calendar days."
            });
        }

        var result = await _reportService.GetSalesReportAsync(
            startDate,
            endDate,
            cancellationToken);

        return Ok(result);
    }
    /// <summary>
    /// Returns branch batch threshold alerts for inventory review.
    /// </summary>
    [HttpGet("low-stock")]
    public async Task<ActionResult<List<LowStockBatchDto>>> GetLowStock(
        CancellationToken cancellationToken)
    {
        var result = await _reportService.GetLowStockBatchesAsync(
            cancellationToken);

        return Ok(result);
    }
    /// <summary>
    /// Returns branch expiry-management summaries and lot details.
    /// </summary>
    [Authorize(Policy = ApplicationPolicies.ViewExpiry)]
    [HttpGet("expiry")]
    public async Task<ActionResult<ExpiryReportDto>> GetExpiryReport(
        CancellationToken cancellationToken)
    {
        var result = await _reportService.GetExpiryReportAsync(
            cancellationToken);

        return Ok(result);
    }
}

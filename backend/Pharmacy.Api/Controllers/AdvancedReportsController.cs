using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Application.Authorization;
using Pharmacy.Application.Reports;

namespace Pharmacy.Api.Controllers;

/// <summary>
/// Exposes branch report tables and native Excel/PDF downloads using the reporting application service.
/// </summary>
[ApiController]
[Authorize(Policy = ApplicationPolicies.ViewReports)]
[Route("api/reports/advanced")]
public sealed class AdvancedReportsController(IAdvancedReportService service) : ControllerBase
{
    /// <summary>
    /// Returns supported tabular report data for the validated branch and date range.
    /// </summary>
    [HttpGet("{kind}")]
    public async Task<IActionResult> Get(string kind, DateOnly startDate, DateOnly endDate, CancellationToken ct)
    {
        try { return Ok(await service.GetAsync(kind, startDate, endDate, ct)); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }
    /// <summary>
    /// Returns a generated Excel/PDF file using the same validated report query as the screen.
    /// </summary>
    [HttpGet("{kind}/export/{format}")]
    public async Task<IActionResult> Export(string kind, string format, DateOnly startDate, DateOnly endDate, CancellationToken ct)
    {
        try
        {
            var file = await service.ExportAsync(kind, startDate, endDate, format, ct);
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }
}

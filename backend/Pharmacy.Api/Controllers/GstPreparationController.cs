using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Reports;

namespace Pharmacy.Api.Controllers;

/// <summary>
/// Exposes administrator-only branch GST worksheet annotation, reconciliation, review and export operations.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/gst-preparation/{year:int}/{month:int}")]
public sealed class GstPreparationController(IGstPreparationRepository repository, GstPreparationService service) : ControllerBase
{
    private string Actor => User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? throw new InvalidOperationException("Authenticated identity required.");
    /// <summary>
    /// Loads the active branch&apos;s period worksheet, completeness issues and reconciliation state.
    /// </summary>
    [HttpGet]
    public Task<IActionResult> Get(int year, int month, CancellationToken ct) => Run(async () => Ok(await repository.GetAsync(year, month, ct)));
    /// <summary>
    /// Saves separate tax annotations with actor and revision information rather than editing operational invoices.
    /// </summary>
    [HttpPut("documents/{kind}/{id:long}")]
    public Task<IActionResult> Save(int year, int month, string kind, long id, SaveGstTaxDetails request, CancellationToken ct) => Run(async () => {
        await repository.SaveDetailsAsync(year, month, kind, id, request, Actor, ct); return NoContent();
    });
    /// <summary>
    /// Carries normalized GSTR-2B CSV and the expected worksheet revision for an atomic draft statement replacement.
    /// </summary>
    public sealed record ImportRequest(string Csv, Guid? Revision);
    /// <summary>
    /// Replaces the editable period&apos;s normalized GSTR-2B statement after complete validation.
    /// </summary>
    [HttpPost("2b/import")]
    [RequestSizeLimit(1_100_000)]
    public Task<IActionResult> Import(int year, int month, ImportRequest request, CancellationToken ct) => Run(async () => {
        await repository.ImportAsync(year, month, request.Csv, request.Revision, Actor, ct); return NoContent();
    });
    /// <summary>
    /// Confirms the expected source fingerprint and saves an internal reviewed snapshot.
    /// </summary>
    [HttpPost("review")]
    public Task<IActionResult> Review(int year, int month, GstReviewRequest request, CancellationToken ct) => Run(async () => Ok(await repository.ReviewAsync(year, month, request, Actor, ct)));
    /// <summary>
    /// Reopens annotation editing using an audited reason and expected worksheet revision.
    /// </summary>
    [HttpPost("reopen")]
    public Task<IActionResult> Reopen(int year, int month, GstReopenRequest request, CancellationToken ct) => Run(async () => {
        await repository.ReopenAsync(year, month, request, Actor, ct); return NoContent();
    });
    /// <summary>
    /// Downloads an internal accountant worksheet and rejects stale reviewed data.
    /// </summary>
    [HttpGet("export")]
    public Task<IActionResult> Export(int year, int month, string format, CancellationToken ct) => Run(async () => {
        var file = await service.ExportAsync(year, month, format, ct); return File(file.Content, file.ContentType, file.FileName);
    });
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "Worksheet changed. Reload before saving." }); }
    }
}

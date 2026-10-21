using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Pharmacy.Application.Assistant;
using Pharmacy.Infrastructure.Assistant;

namespace Pharmacy.Api.Controllers;

/// <summary>
/// Exposes role-protected advisory AI endpoints with bounded requests, explicit provider errors and no operational write actions.
/// </summary>
[ApiController, Route("api/ai")]
[Authorize(Roles = "Admin,Pharmacist,Cashier")]
[EnableRateLimiting("help-assistant")]
public sealed class AiController(AiFeatureService features, ISemanticKnowledge knowledge, IOptions<AiOptions> options) : ControllerBase
{
    /// <summary>
    /// Reports configured feature flags only, without exposing credentials or claiming that provider connectivity has been verified.
    /// </summary>
    [HttpGet("status")]
    public IActionResult Status() => Ok(new
    {
        modelConfigured = options.Value.Enabled && !string.IsNullOrWhiteSpace(options.Value.Model),
        visionConfigured = options.Value.Enabled && !string.IsNullOrWhiteSpace(options.Value.VisionModel),
        vectorConfigured = options.Value.Enabled && options.Value.EnableVectorSearch && !string.IsNullOrWhiteSpace(options.Value.EmbeddingModel),
        note = "Configuration flags only; service connectivity and model availability are verified when used."
    });

    /// <summary>
    /// Returns allow-listed branch inventory queries; Cashiers receive stock facts rather than financial cost data.
    /// </summary>
    [HttpPost("inventory")]
    public Task<ActionResult<InventoryAnswer>> Inventory(InventoryQuestion request, CancellationToken ct) => Run(() => features.InventoryAsync(request, ct));

    /// <summary>
    /// Returns deterministic branch reorder suggestions to Admins and Pharmacists without creating purchase orders.
    /// </summary>
    [HttpGet("reorders"), Authorize(Roles = "Admin,Pharmacist")]
    public Task<ActionResult<IReadOnlyList<ReorderSuggestion>>> Reorders([FromQuery] int historyDays = 30, [FromQuery] int horizonDays = 30, CancellationToken ct = default) =>
        Run(() => features.ReorderAsync(historyDays, horizonDays, ct));

    /// <summary>
    /// Returns verified branch report facts with an optional advisory explanation to reporting-authorized roles.
    /// </summary>
    [HttpGet("sales-insights"), Authorize(Roles = "Admin,Pharmacist")]
    public Task<ActionResult<SalesInsight>> Sales([FromQuery] int days = 30, CancellationToken ct = default) => Run(() => features.SalesAsync(days, ct));

    /// <summary>
    /// Carries bounded anonymized service feedback for transient sentiment analysis rather than persistent customer profiling.
    /// </summary>
    public sealed class FeedbackRequest
    {
        [Required, StringLength(4000)] public string Text { get; set; } = "";
    }
    /// <summary>
    /// Analyzes bounded anonymized feedback for authorized staff without storing a customer feedback history.
    /// </summary>
    [HttpPost("sentiment"), Authorize(Roles = "Admin,Pharmacist")]
    public Task<ActionResult<SentimentResult>> Sentiment(FeedbackRequest request, CancellationToken ct) => Run(() => features.SentimentAsync(request.Text, ct));

    /// <summary>
    /// Returns an unsaved supplier-document extraction for mandatory review by purchasing-authorized staff.
    /// </summary>
    [HttpPost("extract-invoice"), Authorize(Roles = "Admin,Pharmacist"), RequestSizeLimit(6_000_000)]
    public Task<ActionResult<InvoiceExtraction>> Extract(InvoiceExtractionRequest request, CancellationToken ct) => Run(() => features.ExtractAsync(request, ct));

    /// <summary>
    /// Allows only an Admin to embed the packaged guide; operational records are never submitted to the vector index.
    /// </summary>
    [HttpPost("knowledge/index"), Authorize(Roles = "Admin")]
    public Task<ActionResult<object>> Index(CancellationToken ct) => Run<object>(async () => new { indexedChunks = await knowledge.IndexAsync(ct), message = "Current user guide indexed. No business records were indexed." });

    private async Task<ActionResult<T>> Run<T>(Func<Task<T>> operation)
    {
        try { return Ok(await operation()); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (AiUnavailableException ex) { return StatusCode(503, new { message = ex.Message }); }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException)
        { return StatusCode(503, new { message = "AI service returned unexpected data. No operational records were changed." }); }
    }
}

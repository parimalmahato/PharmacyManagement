using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pharmacy.Application.Assistant;

namespace Pharmacy.Api.Controllers;

/// <summary>
/// Exposes authenticated, rate-limited guide questions while rejecting missing account identity and invalid question input.
/// </summary>
[ApiController]
[Route("api/help-assistant")]
[Authorize(Roles = "Admin,Pharmacist,Cashier")]
[EnableRateLimiting("help-assistant")]
public sealed class HelpAssistantController(HelpAssistant assistant) : ControllerBase
{
    /// <summary>
    /// Carries a bounded application-usage question; account identity and permissions come from the authenticated request.
    /// </summary>
    public sealed class QuestionRequest
    {
        [Required, StringLength(1000, MinimumLength = 1)]
        public string Question { get; set; } = "";
    }

    /// <summary>
    /// Validates the authenticated account and question before retrieving approved documentation and optionally generating guidance.
    /// </summary>
    [HttpPost("ask")]
    [RequestSizeLimit(8192)]
    public async Task<ActionResult<HelpAnswer>> Ask(QuestionRequest request, CancellationToken ct)
    {
        if (User.FindFirstValue("sub") is null && User.FindFirstValue(ClaimTypes.NameIdentifier) is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Question)) return BadRequest(new { message = "Enter an application question." });
        return Ok(await assistant.AskAsync(request.Question, ct));
    }
}

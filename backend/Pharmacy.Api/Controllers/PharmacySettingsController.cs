using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Settings;
using Pharmacy.Application.Authorization;
namespace Pharmacy.Api.Controllers;

/// <summary>
/// Exposes administrator-managed branch configuration using validated, revision-aware settings operations.
/// </summary>
[ApiController]
[Route("api/pharmacy-settings")]
[Authorize(Policy = ApplicationPolicies.ManagePharmacySettings)]
public sealed class PharmacySettingsController(IPharmacySettingsRepository repository) : ControllerBase
{
    /// <summary>
    /// Returns the active branch profile or its configuration-not-found response.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PharmacySettingsDto>> Get(CancellationToken ct)
    {
        var result = await repository.GetAsync(ct);
        return result is null ? NotFound(new { message = "Pharmacy settings are not configured." }) : Ok(result);
    }
    /// <summary>
    /// Validates and saves authorized branch settings through revision-aware persistence.
    /// </summary>
    [HttpPut]
    public async Task<ActionResult<PharmacySettingsDto>> Update(UpdatePharmacySettingsRequest request, CancellationToken ct)
    {
        try { return Ok(await repository.UpdateAsync(request, ct)); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "Settings changed. Reload before saving." }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}
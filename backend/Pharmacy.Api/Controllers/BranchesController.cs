using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Authentication;
using Pharmacy.Application.Auditing;
using Pharmacy.Application.Authorization;
using Pharmacy.Application.Branches;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Identity;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Api.Controllers;

/// <summary>
/// Manages branch records and issues validated branch-switch tokens rather than accepting arbitrary client branch claims.
/// </summary>
[ApiController]
[Route("api/branches")]
public sealed class BranchesController(PharmacyDbContext db, UserManager<ApplicationUser> users, JwtTokenService tokens, IAuditService audit) : ControllerBase
{
    /// <summary>
    /// Returns the branch directory available to the authorized management workflow.
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var query = db.Branches.AsNoTracking();
        if (!User.IsInRole("Admin")) query = query.Where(x => x.Id == db.CurrentBranchId);
        return Ok(await query.OrderBy(x => x.Name).Select(x => new BranchDto(x.Id, x.Code, x.Name, x.IsActive)).ToListAsync(ct));
    }
    /// <summary>
    /// Creates an operating branch with validated code and name; it does not copy another branch&apos;s stock or settings.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = ApplicationPolicies.ManageUsers)]
    public async Task<IActionResult> Create(CreateBranchRequest request, CancellationToken ct)
    {
        var code = request.Code?.Trim().ToUpperInvariant(); var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(code) || !System.Text.RegularExpressions.Regex.IsMatch(code, "^[A-Z0-9-]{1,20}$") || string.IsNullOrWhiteSpace(name) || name.Length > 150)
            return BadRequest(new { message = "Enter a branch code (letters/digits/hyphens, up to 20) and a name up to 150 characters." });
        if (await db.Branches.AnyAsync(x => x.Code == code, ct)) return Conflict(new { message = "Branch code already exists." });
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var branch = new Branch { Code = code, Name = name }; db.Branches.Add(branch);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Conflict(new { message = "Branch code already exists or could not be saved." }); }
        await audit.WriteAsync("BranchCreated", "Branch", branch.Id.ToString(), newValues: new { branch.Code, branch.Name }, saveImmediately: false, cancellationToken: ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return Created($"/api/branches/{branch.Id}", new BranchDto(branch.Id, branch.Code, branch.Name, branch.IsActive));
    }
    /// <summary>
    /// Validates branch access and returns a newly signed token for the selected active branch.
    /// </summary>
    [HttpPost("{id:int}/switch")]
    [Authorize(Policy = ApplicationPolicies.ManageUsers)]
    public async Task<IActionResult> Switch(int id, CancellationToken ct)
    {
        var branch = await db.Branches.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
        if (branch is null) return NotFound(new { message = "Active branch not found." });
        var user = await users.FindByIdAsync(User.FindFirst("sub")!.Value);
        if (user is null) return Unauthorized();
        var (token, expiry) = await tokens.GenerateTokenAsync(user, id);
        await audit.WriteAsync("BranchSwitched", "Branch", id.ToString(), newValues: new { branch.Code }, cancellationToken: ct);
        return Ok(new LoginResponse(token, "Bearer", expiry, user.FullName, user.Email ?? "", (await users.GetRolesAsync(user)).ToList(), user.MustChangePassword, id));
    }
}

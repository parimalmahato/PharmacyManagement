
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Authorization;
using Pharmacy.Application.Users;
using Pharmacy.Infrastructure.Identity;
using Pharmacy.Application.Auditing;
using Pharmacy.Infrastructure.Persistence;
namespace Pharmacy.Api.Controllers;

/// <summary>
/// Provides administrator-only employee onboarding, branch assignment, activation and temporary-password resets.
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize(Policy = ApplicationPolicies.ManageUsers)]
public sealed class UsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;
    private readonly PharmacyDbContext _db;
    public UsersController(
        UserManager<ApplicationUser> userManager,
        IAuditService auditService, PharmacyDbContext db)
    {
        _userManager = userManager;
        _auditService = auditService;
        _db = db;
    }

    /// <summary>
    /// Lists employee account status, roles and branch assignment for administrator management.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<EmployeeDto>>> GetAll()
    {
        var users = await _userManager.Users
            .AsNoTracking()
            .OrderBy(x => x.FullName)
            .ToListAsync();

        var result = new List<EmployeeDto>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);

            result.Add(new EmployeeDto(
                user.Id,
                user.FullName,
                user.Email ?? "",
                user.IsActive,
                roles.ToList(),
                user.CreatedAtUtc, user.BranchId
            ));
        }

        return Ok(result);
    }

    /// <summary>
    /// Creates an authorized employee role with a branch and temporary password that must be changed on initial sign-in.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<EmployeeDto>> Create(
        [FromBody] CreateEmployeeRequest request)
    {
        var allowedRoles = new[]
        {
            ApplicationRoles.Pharmacist,
            ApplicationRoles.Cashier
        };

        if (!allowedRoles.Contains(request.Role))
        {
            return BadRequest(new
            {
                message = "Invalid employee role."
            });
        }

        var branchId = request.BranchId ?? _db.CurrentBranchId;
        if (!await _db.Branches.AnyAsync(x => x.Id == branchId && x.IsActive)) return BadRequest(new { message = "Choose an active branch." });
        var email = request.Email.Trim();

        var existingUser =
            await _userManager.FindByEmailAsync(email);

        if (existingUser is not null)
        {
            return Conflict(new
            {
                message = "An account with this email already exists."
            });
        }

        var user = new ApplicationUser
        {
            UserName = email,
            BranchId = branchId,
            Email = email,
            FullName = request.FullName.Trim(),
            IsActive = true,
            MustChangePassword = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(
            user,
            request.Password);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = string.Join(
                    ", ",
                    result.Errors.Select(x => x.Description))
            });
        }

        var roleResult = await _userManager.AddToRoleAsync(
            user,
            request.Role);

        if (!roleResult.Succeeded)
        {
            // Roll back user creation if role assignment fails.
            await _userManager.DeleteAsync(user);

            return BadRequest(new
            {
                message = "Unable to assign employee role."
            });
        }
        
        await _auditService.WriteAsync(
            action: "EmployeeCreated",
            entityType: "ApplicationUser",
            entityId: user.Id,
            newValues: new
            {
                user.FullName,
                user.Email,
                Role = request.Role,
                user.IsActive
            });


        return Created(
            $"/api/users/{user.Id}",
            new EmployeeDto(
                user.Id,
                user.FullName,
                user.Email ?? "",
                user.IsActive,
                new List<string> { request.Role },
                user.CreatedAtUtc, user.BranchId
            )
        );
    }

    /// <summary>
    /// Changes an eligible employee&apos;s validated branch assignment and invalidates stale access context.
    /// </summary>
    [HttpPut("{id}/branch")]
    public async Task<IActionResult> UpdateBranch(string id, [FromBody] EmployeeBranchRequest request)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (!await _db.Branches.AnyAsync(x => x.Id == request.BranchId && x.IsActive)) return BadRequest(new { message = "Choose an active branch." });
        var oldBranchId = user.BranchId; user.BranchId = request.BranchId;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded) return BadRequest(new { message = "Unable to assign branch." });
        await _userManager.UpdateSecurityStampAsync(user);
        await _auditService.WriteAsync("EmployeeBranchChanged", "ApplicationUser", id, oldValues: new { BranchId = oldBranchId }, newValues: new { request.BranchId });
        return NoContent();
    }

    /// <summary>
    /// Activates or deactivates an eligible employee without deleting historical actions.
    /// </summary>
    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(
        string id,
        [FromBody] UpdateEmployeeStatusRequest request)
    {
        var user = await _userManager.FindByIdAsync(id);

        if (user is null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);

        // Prevent disabling administrator accounts here.
        if (roles.Contains(ApplicationRoles.Admin))
        {
            return BadRequest(new
            {
                message = "Administrator accounts cannot be changed here."
            });
        }

        var previousStatus = user.IsActive;
        user.IsActive = request.IsActive;
        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = "Unable to update employee status."
            });
        }
        
        await _auditService.WriteAsync(
            action: request.IsActive
                ? "EmployeeActivated"
                : "EmployeeDeactivated",
            entityType: "ApplicationUser",
            entityId: user.Id,
            oldValues: new
            {
                IsActive = previousStatus
            },
            newValues: new
            {
                user.IsActive
            });

        return NoContent();
    }
    
    /// <summary>
    /// Sets an eligible employee&apos;s temporary password and mandatory-change state, invalidating previous sessions.
    /// </summary>
    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(
        string id,
        [FromBody] ResetEmployeePasswordRequest request)
    {
        var user = await _userManager.FindByIdAsync(id);

        if (user is null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);

        if (roles.Contains(ApplicationRoles.Admin))
        {
            return BadRequest(new
            {
                message =
                    "Administrator passwords cannot be reset here."
            });
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();
        var token =
            await _userManager.GeneratePasswordResetTokenAsync(user);

        var result = await _userManager.ResetPasswordAsync(
            user,
            token,
            request.NewPassword);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = string.Join(
                    ", ",
                    result.Errors.Select(e => e.Description))
            });
        }
        user.MustChangePassword = true;

        var updateResult = await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            return StatusCode(500, new
            {
                message = "Password reset could not be completed. No changes were saved."
            });
        }

        await _auditService.WriteAsync(
            action: "EmployeePasswordReset",
            entityType: "ApplicationUser",
            entityId: user.Id);
        await transaction.CommitAsync();
        return NoContent();
    }

}

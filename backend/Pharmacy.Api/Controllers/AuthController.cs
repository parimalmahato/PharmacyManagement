
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Application.Authentication;
using Pharmacy.Infrastructure.Identity;
using Pharmacy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Pharmacy.Api.Controllers;

/// <summary>
/// Authenticates accounts and handles own-password changes with identity validation and session invalidation rules.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly JwtTokenService _jwtTokenService;
    private readonly PharmacyDbContext _db;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        JwtTokenService jwtTokenService, PharmacyDbContext db)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenService = jwtTokenService;
        _db = db;
    }

    /// <summary>
    /// Validates account credentials and state and returns a short-lived token for the assigned branch and roles.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new
            {
                message = "Email and password are required."
            });
        }

        var user = await _userManager.FindByEmailAsync(
            request.Email.Trim());

        if (user is null || !user.IsActive)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        var result = await _signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }
        if (!user.IsActive)
        {
            return Unauthorized(new
            {
                message = "This account is inactive."
            });
        }
        if (!await _db.Branches.AnyAsync(x => x.Id == user.BranchId && x.IsActive))
            return Unauthorized(new { message = "Assigned branch is unavailable. Contact your administrator." });
        var (token, expiresAtUtc) =
            await _jwtTokenService.GenerateTokenAsync(user);

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new LoginResponse(
            token,
            "Bearer",
            expiresAtUtc,
            user.FullName,
            user.Email ?? "",
            roles.ToList(),
            user.MustChangePassword,
            user.BranchId
        ));
    }

    /// <summary>
    /// Returns the authenticated account view used by the client to restore its authorized workspace.
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = User.FindFirst(
            System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub
        )?.Value;

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userId);

        if (user is null || !user.IsActive)
        {
            return Unauthorized();
        }

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new
        {
            user.Id,
            user.FullName,
            user.Email,
            Roles = roles
        });
    }
    
    /// <summary>
    /// Validates the current credential, updates the password and invalidates prior sessions through identity state changes.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request)
    {
        var userId = User.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userId);

        if (user is null || !user.IsActive)
        {
            return Unauthorized();
        }

        var result = await _userManager.ChangePasswordAsync(
            user,
            request.CurrentPassword,
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

        user.MustChangePassword = false;

        var updateResult = await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            return StatusCode(500, new
            {
                message =
                    "Password changed, but account update failed."
            });
        }

        await _userManager.UpdateSecurityStampAsync(user);

        return Ok(new ChangePasswordResponse(
            "Password changed successfully. Please log in again."
        ));
    }           

}


using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
namespace Pharmacy.Infrastructure.Identity;

/// <summary>
/// Creates short-lived signed access tokens containing account, role, branch and security-stamp claims.
/// </summary>
public sealed class JwtTokenService
{
    private readonly IConfiguration _configuration;
    private readonly UserManager<ApplicationUser> _userManager;

    public JwtTokenService(
        IConfiguration configuration,
        UserManager<ApplicationUser> userManager)
    {
        _configuration = configuration;
        _userManager = userManager;
    }

    /// <summary>
    /// Signs a short-lived access token containing current roles, branch and security stamp so later account changes can invalidate access.
    /// </summary>
    public async Task<(string Token, DateTime ExpiresAtUtc)>
        GenerateTokenAsync(ApplicationUser user, int? branchId = null)
    {
        var roles = await _userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("name", user.FullName),
            new("branch_id", (branchId ?? user.BranchId).ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(JwtRegisteredClaimNames.Email, user.Email ?? "")
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim("role", role));
            claims.Add(new Claim(
                        "security_stamp",
                        user.SecurityStamp ?? string.Empty
                    ));
        }

        var key = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "JWT key is not configured.");

        var issuer = _configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException(
                "JWT issuer is not configured.");

        var audience = _configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException(
                "JWT audience is not configured.");

        var minutes = 15;

        var configuredMinutes =
            _configuration["Jwt:AccessTokenMinutes"];

        if (!string.IsNullOrWhiteSpace(configuredMinutes))
        {
            if (!int.TryParse(configuredMinutes, out minutes))
            {
                throw new InvalidOperationException(
                    "Jwt:AccessTokenMinutes must be a valid integer.");
            }
        }

        if (minutes < 1 || minutes > 60)
        {
            throw new InvalidOperationException(
                "Access token lifetime must be between 1 and 60 minutes.");
        }

        var expiresAtUtc = DateTime.UtcNow.AddMinutes(minutes);

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return (
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAtUtc
        );
    }
}

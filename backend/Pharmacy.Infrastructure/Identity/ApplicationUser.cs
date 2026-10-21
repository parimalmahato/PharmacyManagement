
using Microsoft.AspNetCore.Identity;

namespace Pharmacy.Infrastructure.Identity;

/// <summary>
/// Extends the identity account with pharmacy role-related profile, branch assignment, activation and password-change state.
/// </summary>
public sealed class ApplicationUser : IdentityUser
{
    public int BranchId { get; set; } = 1;
    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = false;
    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;
}

using System.ComponentModel.DataAnnotations;
namespace Pharmacy.Application.Authentication;
/// <summary>
/// Credentials submitted to the authenticated application entry point; passwords must not be logged.
/// </summary>
public sealed record LoginRequest(
    string Email,
    string Password
);

/// <summary>
/// Successful sign-in data including the short-lived access token and account state required by the frontend.
/// </summary>
public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAtUtc,
    string FullName,
    string Email,
    List<string> Roles,
    bool MustChangePassword,
    int BranchId = 1
);
/// <summary>
/// Current and replacement credentials for changing the authenticated user&apos;s own password.
/// </summary>
public sealed record ChangePasswordRequest(
    [Required] 
    string CurrentPassword,
    [Required] 
    string NewPassword
);

/// <summary>
/// Reports the outcome of an own-password change so the client can refresh its authentication state.
/// </summary>
public sealed record ChangePasswordResponse(
    string Message
);

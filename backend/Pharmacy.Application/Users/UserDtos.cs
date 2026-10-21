
using System.ComponentModel.DataAnnotations;

namespace Pharmacy.Application.Users;
/// <summary>
/// Requests an employee branch assignment; authorization and target-branch validation occur on the server.
/// </summary>
public sealed record EmployeeBranchRequest(int BranchId);

/// <summary>
/// Administrator input for a Cashier or Pharmacist account with an initial password and assigned branch.
/// </summary>
public sealed record CreateEmployeeRequest(
    [Required, MaxLength(150)] string FullName,
    [Required, EmailAddress] string Email,
    [Required] string Password,
    [Required] string Role,
    int? BranchId = null
);

/// <summary>
/// Requests employee activation or deactivation without deleting historical activity.
/// </summary>
public sealed record UpdateEmployeeStatusRequest(
    bool IsActive
);

/// <summary>
/// Employee-management view of account identity, assigned branch, role and activation state, without password material.
/// </summary>
public sealed record EmployeeDto(
    string Id,
    string FullName,
    string Email,
    bool IsActive,
    List<string> Roles,
    DateTime CreatedAtUtc,
    int BranchId = 1
);
/// <summary>
/// Administrator-supplied temporary password for an employee who must choose a replacement at next sign-in.
/// </summary>
public sealed record ResetEmployeePasswordRequest(
    [Required] string NewPassword
);

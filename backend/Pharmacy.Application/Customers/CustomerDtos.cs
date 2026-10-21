
namespace Pharmacy.Application.Customers;

/// <summary>
/// Input for a customer profile in the current branch.
/// </summary>
public sealed record CreateCustomerRequest(
    string Name,
    string? Phone,
    string? Email,
    string? Address);

/// <summary>
/// Updated branch customer contact details without modifying historical sale records.
/// </summary>
public sealed record UpdateCustomerRequest(
    string Name,
    string? Phone,
    string? Email,
    string? Address);

/// <summary>
/// Customer profile data returned to branch customer-management and billing screens.
/// </summary>
public sealed record CustomerDto(
    int Id,
    string Name,
    string? Phone,
    string? Email,
    string? Address,
    DateTime CreatedAtUtc,
    bool IsActive);

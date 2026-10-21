
namespace Pharmacy.Application.Customers;

/// <summary>
/// Defines branch customer maintenance without removing historical invoice references.
/// </summary>
public interface ICustomerRepository
{
    /// <summary>
    /// Reads customer profiles from the active branch.
    /// </summary>
    Task<List<CustomerDto>> GetAllAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a branch-visible customer by identifier or returns null when absent.
    /// </summary>
    Task<CustomerDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a customer profile and saves it under the current branch without creating a sale.
    /// </summary>
    Task<CustomerDto> CreateAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an active branch-visible customer profile or returns null when no eligible record exists.
    /// </summary>
    Task<CustomerDto?> UpdateAsync(
        int id,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deactivates an active branch-visible customer and preserves historical invoice references.
    /// </summary>
    Task<bool> DeactivateAsync(
        int id,
        CancellationToken cancellationToken = default);
}

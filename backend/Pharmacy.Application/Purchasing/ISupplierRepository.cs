using Pharmacy.Domain.Entities.Purchasing;

namespace Pharmacy.Application.Purchasing;

/// <summary>
/// Defines access to the shared supplier catalog used by branch purchase workflows.
/// </summary>
public interface ISupplierRepository
{
    /// <summary>
    /// Reads the shared supplier directory used across branch purchase workflows.
    /// </summary>
    Task<List<Supplier>> GetAllAsync(CancellationToken cancellationToken);
    /// <summary>
    /// Returns the shared supplier matching an identifier or null when absent.
    /// </summary>
    Task<Supplier?> GetByIdAsync(int id, CancellationToken cancellationToken);
    /// <summary>
    /// Stages a shared supplier profile for persistence.
    /// </summary>
    Task AddAsync(Supplier supplier, CancellationToken cancellationToken);
    /// <summary>
    /// Persists staged supplier changes through the current unit of work.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
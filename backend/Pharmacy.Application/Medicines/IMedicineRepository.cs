
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Medicines;

/// <summary>
/// Defines storage operations for the shared medicine catalog rather than branch-owned batch quantities.
/// </summary>
public interface IMedicineRepository
{
    /// <summary>
    /// Reads shared medicine catalog entries; quantities and branch stock are obtained through batch queries.
    /// </summary>
    Task<IReadOnlyList<Medicine>> GetAllAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Loads a shared catalog entry by identifier or returns null when it is absent.
    /// </summary>
    Task<Medicine?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stages a new shared catalog entry; persistence is completed by the surrounding use case.
    /// </summary>
    Task AddAsync(
        Medicine medicine,
        CancellationToken cancellationToken);

    /// <summary>
    /// Persists tracked catalog and audit changes in the current unit of work.
    /// </summary>
    Task SaveChangesAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Runs the supplied catalog-and-audit operation within the repository&apos;s database transaction boundary.
    /// </summary>
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken);
}

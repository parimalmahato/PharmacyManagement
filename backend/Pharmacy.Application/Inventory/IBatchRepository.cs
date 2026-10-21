
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Inventory;

/// <summary>
/// Defines branch batch storage and transaction boundaries used by opening-stock registration.
/// </summary>
public interface IBatchRepository
{
    /// <summary>
    /// Reads medicine batches visible through the current branch filters.
    /// </summary>
    Task<IReadOnlyList<MedicineBatch>> GetAllAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Loads a branch-visible batch and its medicine details or returns null when absent.
    /// </summary>
    Task<MedicineBatch?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    /// <summary>
    /// Checks that the shared catalog medicine exists and is active before opening stock can reference it.
    /// </summary>
    Task<bool> MedicineExistsAsync(
        int medicineId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stages a new lot in the current branch; the caller coordinates persistence and audit recording.
    /// </summary>
    Task AddAsync(
        MedicineBatch batch,
        CancellationToken cancellationToken);

    /// <summary>
    /// Persists tracked batch and audit changes under the current branch ownership rules.
    /// </summary>
    Task SaveChangesAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Runs opening-stock and audit operations together within a database transaction.
    /// </summary>
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken);
}

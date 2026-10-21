
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Inventory;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Inventory;

/// <summary>
/// Persists branch-filtered medicine batches and supplies transactional execution for stock and audit changes.
/// </summary>
public sealed class BatchRepository(
    PharmacyDbContext dbContext) : IBatchRepository
{
    /// <summary>
    /// Reads medicine batches visible through the current branch filters.
    /// </summary>
    public async Task<IReadOnlyList<MedicineBatch>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.MedicineBatches
            .Include(b => b.Medicine)
            .AsNoTracking()
            .OrderBy(b => b.ExpiryDate)
            .ThenBy(b => b.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Loads a branch-visible batch and its medicine details or returns null when absent.
    /// </summary>
    public Task<MedicineBatch?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return dbContext.MedicineBatches
            .Include(b => b.Medicine)
            .FirstOrDefaultAsync(
                b => b.Id == id,
                cancellationToken);
    }

    /// <summary>
    /// Checks that the shared catalog medicine exists and is active before opening stock can reference it.
    /// </summary>
    public Task<bool> MedicineExistsAsync(
        int medicineId,
        CancellationToken cancellationToken)
    {
        return dbContext.Medicines.AnyAsync(
            m => m.Id == medicineId && m.IsActive,
            cancellationToken);
    }

    /// <summary>
    /// Stages a new lot in the current branch; the caller coordinates persistence and audit recording.
    /// </summary>
    public async Task AddAsync(
        MedicineBatch batch,
        CancellationToken cancellationToken)
    {
        await dbContext.MedicineBatches.AddAsync(
            batch,
            cancellationToken);
    }

    /// <summary>
    /// Persists tracked batch and audit changes under the current branch ownership rules.
    /// </summary>
    public async Task SaveChangesAsync(
        CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(
            cancellationToken);
    }

    /// <summary>
    /// Runs opening-stock and audit operations together within a database transaction.
    /// </summary>
    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        try
        {
            // Execute batch creation and audit logging.
            await operation(cancellationToken);

            // Save remaining changes, including audit logs.
            await dbContext.SaveChangesAsync(
                cancellationToken);

            // Commit batch and audit together.
            await transaction.CommitAsync(
                cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(
                CancellationToken.None);

            throw;
        }
    }
}

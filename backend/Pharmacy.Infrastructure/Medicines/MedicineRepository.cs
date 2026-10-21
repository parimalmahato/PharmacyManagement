
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Medicines;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Medicines;

/// <summary>
/// Uses Entity Framework to read and maintain the shared medicine catalog independently of branch inventory.
/// </summary>
public sealed class MedicineRepository(
    PharmacyDbContext dbContext) : IMedicineRepository
{
    /// <summary>
    /// Reads shared medicine catalog entries; quantities and branch stock are obtained through batch queries.
    /// </summary>
    public async Task<IReadOnlyList<Medicine>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.Medicines
            .AsNoTracking()
            .OrderBy(m => m.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Loads a shared catalog entry by identifier or returns null when it is absent.
    /// </summary>
    public Task<Medicine?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return dbContext.Medicines
            .FirstOrDefaultAsync(
                m => m.Id == id,
                cancellationToken);
    }

    /// <summary>
    /// Stages a new shared catalog entry; persistence is completed by the surrounding use case.
    /// </summary>
    public async Task AddAsync(
        Medicine medicine,
        CancellationToken cancellationToken)
    {
        await dbContext.Medicines.AddAsync(
            medicine,
            cancellationToken);
    }

    /// <summary>
    /// Persists tracked catalog and audit changes in the current unit of work.
    /// </summary>
    public async Task SaveChangesAsync(
        CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(
            cancellationToken);
    }

    /// <summary>
    /// Runs the supplied catalog-and-audit operation within the repository&apos;s database transaction boundary.
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
            await operation(cancellationToken);

            // Persist remaining tracked changes,
            // including audit records.
            await dbContext.SaveChangesAsync(
                cancellationToken);

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

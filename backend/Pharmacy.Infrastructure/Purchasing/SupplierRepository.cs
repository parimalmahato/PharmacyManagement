using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Purchasing;
using Pharmacy.Domain.Entities.Purchasing;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Purchasing;

/// <summary>
/// Reads and stages changes to shared supplier records through Entity Framework.
/// </summary>
public sealed class SupplierRepository(
    PharmacyDbContext dbContext) : ISupplierRepository
{
    /// <summary>
    /// Reads the shared supplier directory used across branch purchase workflows.
    /// </summary>
    public Task<List<Supplier>> GetAllAsync(
        CancellationToken cancellationToken) =>
        dbContext.Suppliers
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Returns the shared supplier matching an identifier or null when absent.
    /// </summary>
    public Task<Supplier?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken) =>
        dbContext.Suppliers
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    /// <summary>
    /// Stages a shared supplier profile for persistence.
    /// </summary>
    public async Task AddAsync(
        Supplier supplier,
        CancellationToken cancellationToken)
    {
        await dbContext.Suppliers.AddAsync(supplier, cancellationToken);
    }

    /// <summary>
    /// Persists staged supplier changes through the current unit of work.
    /// </summary>
    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
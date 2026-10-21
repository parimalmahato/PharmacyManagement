
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Customers;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Customers;

/// <summary>
/// Reads and maintains customers through the current branch&apos;s persistence filters.
/// </summary>
public sealed class CustomerRepository(
    PharmacyDbContext dbContext) : ICustomerRepository
{
    /// <summary>
    /// Reads customer profiles from the active branch.
    /// </summary>
    public async Task<List<CustomerDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Customers
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new CustomerDto(
                x.Id,
                x.Name,
                x.Phone,
                x.Email,
                x.Address,
                x.CreatedAtUtc,
                x.IsActive))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Loads a branch-visible customer by identifier or returns null when absent.
    /// </summary>
    public async Task<CustomerDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Customers
            .AsNoTracking()
            .Where(x => x.Id == id && x.IsActive)
            .Select(x => new CustomerDto(
                x.Id,
                x.Name,
                x.Phone,
                x.Email,
                x.Address,
                x.CreatedAtUtc,
                x.IsActive))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Validates a customer profile and saves it under the current branch without creating a sale.
    /// </summary>
    public async Task<CustomerDto> CreateAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        var customer = new Customer(
            request.Name,
            request.Phone,
            request.Email,
            request.Address);

        dbContext.Customers.Add(customer);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(customer);
    }

    /// <summary>
    /// Updates an active branch-visible customer profile or returns null when no eligible record exists.
    /// </summary>
    public async Task<CustomerDto?> UpdateAsync(
        int id,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(
                x => x.Id == id && x.IsActive,
                cancellationToken);

        if (customer is null)
        {
            return null;
        }

        customer.UpdateDetails(
            request.Name,
            request.Phone,
            request.Email,
            request.Address);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(customer);
    }

    /// <summary>
    /// Soft-deactivates an active branch-visible customer and preserves historical invoice references.
    /// </summary>
    public async Task<bool> DeactivateAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(
                x => x.Id == id && x.IsActive,
                cancellationToken);

        if (customer is null)
        {
            return false;
        }

        customer.Deactivate();

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static CustomerDto Map(Customer customer)
    {
        return new CustomerDto(
            customer.Id,
            customer.Name,
            customer.Phone,
            customer.Email,
            customer.Address,
            customer.CreatedAtUtc,
            customer.IsActive);
    }
}

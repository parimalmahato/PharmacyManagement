namespace Pharmacy.Domain.Entities.Purchasing;

/// <summary>
/// A shared supplier-catalog entry; supplier invoices and received stock remain branch-specific.
/// </summary>
public sealed class Supplier
{
    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public bool IsActive { get; private set; } = true;

    private Supplier() { } // EF Core

    public Supplier(
        string name,
        string? phone,
        string? email,
        string? address)
    {
        Update(name, phone, email, address);
    }

    /// <summary>
    /// Validates the shared supplier name and normalizes optional contact fields without changing branch purchase histories.
    /// </summary>
    public void Update(
        string name,
        string? phone,
        string? email,
        string? address)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Supplier name is required.");

        if (name.Trim().Length > 150)
            throw new ArgumentException("Supplier name cannot exceed 150 characters.");

        Name = name.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
    }

    /// <summary>
    /// Soft-deactivates the shared supplier profile while preserving references from supplier invoices.
    /// </summary>
    public void Deactivate() => IsActive = false;
}
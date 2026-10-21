
namespace Pharmacy.Domain.Entities;

/// <summary>
/// A branch customer profile with validated contact details and soft deactivation that preserves historical references.
/// </summary>
public class Customer
{
    public int Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Phone { get; private set; }

    public string? Email { get; private set; }

    public string? Address { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public bool IsActive { get; private set; }

    private Customer()
    {
        // Required by EF Core.
    }

    public Customer(
        string name,
        string? phone = null,
        string? email = null,
        string? address = null)
    {
        UpdateDetails(name, phone, email, address);

        CreatedAtUtc = DateTime.UtcNow;
        IsActive = true;
    }

    /// <summary>
    /// Validates and replaces branch customer contact details without changing historical invoice financial values.
    /// </summary>
    public void UpdateDetails(
        string name,
        string? phone,
        string? email,
        string? address)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Customer name is required.",
                nameof(name));
        }

        if (name.Trim().Length > 150)
        {
            throw new ArgumentException(
                "Customer name cannot exceed 150 characters.",
                nameof(name));
        }

        if (phone?.Trim().Length > 20)
        {
            throw new ArgumentException(
                "Phone cannot exceed 20 characters.",
                nameof(phone));
        }

        if (email?.Trim().Length > 254)
        {
            throw new ArgumentException(
                "Email cannot exceed 254 characters.",
                nameof(email));
        }

        if (address?.Trim().Length > 500)
        {
            throw new ArgumentException(
                "Address cannot exceed 500 characters.",
                nameof(address));
        }

        Name = name.Trim();
        Phone = Normalize(phone);
        Email = Normalize(email);
        Address = Normalize(address);
    }

    /// <summary>
    /// Soft-deactivates the customer so historical sales remain traceable.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}

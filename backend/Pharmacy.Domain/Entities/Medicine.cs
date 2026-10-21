namespace Pharmacy.Domain.Entities;

/// <summary>
/// A shared medicine-catalog entry with descriptive and tax configuration; batch inventory is maintained separately for each branch.
/// </summary>
public class Medicine
{
    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string GenericName { get; private set; } = string.Empty;
    public string Manufacturer { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public string? HsnCode { get; private set; }
    public decimal GstPercentage { get; private set; }

    private Medicine() { } // EF Core

    public Medicine(string name, string genericName, string manufacturer,
        string category, string? hsnCode = null, decimal gstPercentage = 0m)
    {
        Update(name, genericName, manufacturer, category);
        ConfigureTax(hsnCode, gstPercentage);
    }

    /// <summary>
    /// Validates and replaces shared catalog descriptions without changing batch quantities or historical invoice snapshots.
    /// </summary>
    public void Update(string name, string genericName, string manufacturer, string category)
    {
        Name = RequireValue(name, nameof(name), 150);
        GenericName = RequireValue(genericName, nameof(genericName), 150);
        Manufacturer = RequireValue(manufacturer, nameof(manufacturer), 150);
        Category = RequireValue(category, nameof(category), 100);
    }

    /// <summary>
    /// Validates HSN digits and the allowed GST percentage for future sale-line snapshots.
    /// </summary>
    public void ConfigureTax(string? hsnCode, decimal gstPercentage)
    {
        if (gstPercentage < 0m || gstPercentage > 100m)
            throw new ArgumentException("GST percentage must be between 0 and 100.", nameof(gstPercentage));

        var normalizedHsn = string.IsNullOrWhiteSpace(hsnCode) ? null : hsnCode.Trim();
        if (normalizedHsn is not null &&
            (normalizedHsn.Length > 20 || !normalizedHsn.All(char.IsDigit)))
            throw new ArgumentException("HSN code must contain only digits and cannot exceed 20 characters.", nameof(hsnCode));

        HsnCode = normalizedHsn;
        GstPercentage = gstPercentage;
    }

    /// <summary>
    /// Soft-deactivates the catalog entry while preserving references from stock and historical transactions.
    /// </summary>
    public void Deactivate() => IsActive = false;

    private static string RequireValue(string value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{field} is required.");

        value = value.Trim();
        if (value.Length > maxLength)
            throw new ArgumentException($"{field} cannot exceed {maxLength} characters.");

        return value;
    }
}

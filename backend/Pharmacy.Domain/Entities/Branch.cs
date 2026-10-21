namespace Pharmacy.Domain.Entities;

/// <summary>
/// Identifies an operating location whose stock and transactions are isolated while medicine and supplier catalogs are shared.
/// </summary>
public sealed class Branch
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

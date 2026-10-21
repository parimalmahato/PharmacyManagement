
namespace Pharmacy.Domain.Entities.Sales;

/// <summary>
/// Distinguishes a completed invoice from one preserved as cancelled.
/// </summary>
public enum SaleStatus
{
    Completed = 1,
    Cancelled = 2
}

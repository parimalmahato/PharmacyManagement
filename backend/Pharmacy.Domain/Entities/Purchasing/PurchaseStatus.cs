namespace Pharmacy.Domain.Entities.Purchasing;

/// <summary>
/// Tracks whether a supplier invoice is still a draft, has been received into stock or was cancelled.
/// </summary>
public enum PurchaseStatus
{
    Draft = 1,
    Received = 2,
    Cancelled = 3
}
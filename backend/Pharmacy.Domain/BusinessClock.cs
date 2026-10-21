namespace Pharmacy.Domain;

/// <summary>
/// Provides the India Standard Time business date used by domain rules, including expiry eligibility.
/// </summary>
public static class BusinessClock
{
    /// <summary>
    /// Current India Standard Time calendar date used by domain expiry and stock rules.
    /// </summary>
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddMinutes(330));
}

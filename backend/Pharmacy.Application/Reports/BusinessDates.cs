namespace Pharmacy.Application.Reports;

/// <summary>
/// Converts inclusive India business dates into UTC query boundaries and validates bounded report periods.
/// </summary>
public static class BusinessDates
{
    /// <summary>
    /// Current India Standard Time calendar date used for report periods and expiry-related business queries.
    /// </summary>
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddMinutes(330));
    /// <summary>
    /// Validates an inclusive range of at most 366 India business dates and returns an inclusive UTC start and exclusive UTC end for queries.
    /// </summary>
    public static (DateTime StartUtc, DateTime EndUtc) Range(DateOnly start, DateOnly end)
    {
        if (start > end || end.DayNumber - start.DayNumber > 365 || end == DateOnly.MaxValue || start == DateOnly.MinValue)
            throw new ArgumentException("Choose a valid date range of at most 366 days.");
        return (DateTime.SpecifyKind(start.ToDateTime(TimeOnly.MinValue).AddMinutes(-330), DateTimeKind.Utc),
            DateTime.SpecifyKind(end.AddDays(1).ToDateTime(TimeOnly.MinValue).AddMinutes(-330), DateTimeKind.Utc));
    }
}

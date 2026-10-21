
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Notifications;

/// <summary>
/// Creates configured expiry alerts for the current processing branch without selling, disposing of or transferring batches.
/// </summary>
public sealed class ExpiryNotificationProcessor
{
    private readonly PharmacyDbContext _dbContext;
    private readonly ILogger<ExpiryNotificationProcessor> _logger;

    public ExpiryNotificationProcessor(
        PharmacyDbContext dbContext,
        ILogger<ExpiryNotificationProcessor> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Creates missing expiry alerts according to the current processing branch&apos;s settings without changing batch quantities.
    /// </summary>
    public async Task ProcessAsync(
        CancellationToken cancellationToken = default)
    {
        var indiaTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

        var indiaNow = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow,
            indiaTimeZone);

        var today = DateOnly.FromDateTime(indiaNow);
        var settings = await _dbContext.PharmacySettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (settings is { ExpiryAlertsEnabled: false }) return;
        var endDate = today.AddDays(settings?.ExpiryAlertDays ?? 90);

        var batches = await (
            from batch in _dbContext.MedicineBatches.AsNoTracking()
            join medicine in _dbContext.Medicines.AsNoTracking()
                on batch.MedicineId equals medicine.Id
            where batch.Quantity > 0
                  && batch.ExpiryDate <= endDate
            select new
            {
                BatchId = batch.Id,
                MedicineName = medicine.Name,
                batch.BatchNumber,
                batch.ExpiryDate
            }
        ).ToListAsync(cancellationToken);

        var existingAlerts = await _dbContext.ExpiryNotifications
            .AsNoTracking()
            .Select(x => new
            {
                x.MedicineBatchId,
                x.AlertType
            })
            .ToListAsync(cancellationToken);

        var existingKeys = existingAlerts
            .Select(x => (x.MedicineBatchId, x.AlertType))
            .ToHashSet();

        var newNotifications = new List<ExpiryNotification>();

        foreach (var batch in batches)
        {
            var daysRemaining =
                batch.ExpiryDate.DayNumber - today.DayNumber;

            string alertType;

            if (daysRemaining < 0)
                alertType = "Expired";
            else if (daysRemaining <= 30)
                alertType = "Expiring30Days";
            else if (daysRemaining <= 60)
                alertType = "Expiring60Days";
            else if (daysRemaining <= 90)
                alertType = "Expiring90Days";
            else
                alertType = "ExpiringConfiguredWindow";

            var key = (batch.BatchId, alertType);

            if (!existingKeys.Add(key))
                continue;

            var message = daysRemaining < 0
                ? $"{batch.MedicineName} (Batch {batch.BatchNumber}) " +
                  $"expired on {batch.ExpiryDate:yyyy-MM-dd}."
                : $"{batch.MedicineName} (Batch {batch.BatchNumber}) " +
                  $"will expire in {daysRemaining} day(s).";

            newNotifications.Add(
                new ExpiryNotification(
                    batch.BatchId,
                    alertType,
                    message));
        }

        if (newNotifications.Count == 0)
        {
            _logger.LogInformation(
                "Expiry notification check completed. No new alerts.");

            return;
        }

        _dbContext.ExpiryNotifications.AddRange(newNotifications);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created {Count} expiry notifications.",
            newNotifications.Count);
    }
}

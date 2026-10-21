
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Notifications;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Notifications;

/// <summary>
/// Uses branch-filtered persistence to retrieve alerts and mark individual or all notifications as read.
/// </summary>
public sealed class NotificationRepository : INotificationRepository
{
    private readonly PharmacyDbContext _dbContext;

    public NotificationRepository(PharmacyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Returns branch alerts, optionally restricted to unread messages.
    /// </summary>
    public async Task<List<NotificationDto>> GetAllAsync(
        bool unreadOnly,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ExpiryNotifications
            .AsNoTracking()
            .AsQueryable();

        if (unreadOnly)
        {
            query = query.Where(x => !x.IsRead);
        }

        return await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Select(x => new NotificationDto(
                x.Id,
                x.MedicineBatchId,
                x.AlertType,
                x.Message,
                x.CreatedAtUtc,
                x.IsRead))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Counts total and unread alerts for the current branch.
    /// </summary>
    public async Task<NotificationSummaryDto> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var totalCount = await _dbContext.ExpiryNotifications
            .CountAsync(cancellationToken);

        var unreadCount = await _dbContext.ExpiryNotifications
            .CountAsync(x => !x.IsRead, cancellationToken);

        return new NotificationSummaryDto(
            totalCount,
            unreadCount);
    }

    /// <summary>
    /// Acknowledges one branch-visible notification without modifying its source batch.
    /// </summary>
    public async Task<bool> MarkAsReadAsync(
        long notificationId,
        CancellationToken cancellationToken = default)
    {
        var notification = await _dbContext.ExpiryNotifications
            .FirstOrDefaultAsync(
                x => x.Id == notificationId,
                cancellationToken);

        if (notification is null)
        {
            return false;
        }

        notification.MarkAsRead();

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>
    /// Acknowledges unread branch notifications and returns the affected count without changing inventory.
    /// </summary>
    public async Task<int> MarkAllAsReadAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ExpiryNotifications
            .Where(x => !x.IsRead)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    x => x.IsRead,
                    true),
                cancellationToken);
    }
}

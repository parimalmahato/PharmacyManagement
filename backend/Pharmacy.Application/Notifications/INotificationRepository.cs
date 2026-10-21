
namespace Pharmacy.Application.Notifications;

/// <summary>
/// Defines branch expiry-notification reads and acknowledgement operations without modifying stock.
/// </summary>
public interface INotificationRepository
{
    /// <summary>
    /// Returns branch alerts, optionally restricted to unread messages.
    /// </summary>
    Task<List<NotificationDto>> GetAllAsync(
        bool unreadOnly,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts total and unread alerts for the current branch.
    /// </summary>
    Task<NotificationSummaryDto> GetSummaryAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Acknowledges one branch-visible notification without modifying its source batch.
    /// </summary>
    Task<bool> MarkAsReadAsync(
        long notificationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Acknowledges unread branch notifications and returns the affected count without changing inventory.
    /// </summary>
    Task<int> MarkAllAsReadAsync(
        CancellationToken cancellationToken = default);
}

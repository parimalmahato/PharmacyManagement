
namespace Pharmacy.Application.Notifications;

/// <summary>
/// A branch batch-expiry message and its acknowledgement state.
/// </summary>
public sealed record NotificationDto(
    long Id,
    int MedicineBatchId,
    string AlertType,
    string Message,
    DateTime CreatedAtUtc,
    bool IsRead
);

/// <summary>
/// Total and unread branch alert counts used by the notification badge.
/// </summary>
public sealed record NotificationSummaryDto(
    int TotalCount,
    int UnreadCount
);


namespace Pharmacy.Domain.Entities;

/// <summary>
/// A persisted batch-expiry alert whose read status acknowledges the message without changing inventory.
/// </summary>
public sealed class ExpiryNotification
{
    public long Id { get; private set; }

    public int MedicineBatchId { get; private set; }

    public string AlertType { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public bool IsRead { get; private set; }

    private ExpiryNotification() { }

    public ExpiryNotification(
        int medicineBatchId,
        string alertType,
        string message)
    {
        if (medicineBatchId <= 0)
            throw new ArgumentException("Invalid batch ID.");

        if (string.IsNullOrWhiteSpace(alertType))
            throw new ArgumentException("Alert type is required.");

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message is required.");

        MedicineBatchId = medicineBatchId;
        AlertType = alertType.Trim();
        Message = message.Trim();
        CreatedAtUtc = DateTime.UtcNow;
        IsRead = false;
    }

    /// <summary>
    /// Acknowledges the alert without changing the batch, its expiry or its sellable quantity.
    /// </summary>
    public void MarkAsRead()
    {
        IsRead = true;
    }
}

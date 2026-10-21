
namespace Pharmacy.Domain.Entities;

/// <summary>
/// Stores the actor, operation, entity and recorded changes used to trace application activity in a branch.
/// </summary>
public sealed class AuditLog
{
    public long Id { get; set; }

    public string? UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string? EntityId { get; set; }

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;
}

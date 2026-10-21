
namespace Pharmacy.Application.Auditing;

/// <summary>
/// Records structured audit events within the current request and branch, optionally deferring persistence to a surrounding transaction.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Writes an audit event with optional old/new values; deferred saving allows the caller&apos;s surrounding transaction to include the audit.
    /// </summary>
    Task WriteAsync(
        string action,
        string entityType,
        string? entityId = null,
        object? oldValues = null,
        object? newValues = null,
        bool saveImmediately = true,
        CancellationToken cancellationToken = default);
}

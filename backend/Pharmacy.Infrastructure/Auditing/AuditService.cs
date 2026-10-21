
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Pharmacy.Application.Auditing;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Auditing;

/// <summary>
/// Captures request actor and branch context when writing audit entries, including operations saved with business transactions.
/// </summary>
public sealed class AuditService : IAuditService
{
    private readonly PharmacyDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(
        PharmacyDbContext db,
        IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Captures structured actor and entity changes, optionally deferring SaveChanges so the audit event commits with its business transaction.
    /// </summary>
    public async Task WriteAsync(
        string action,
        string entityType,
        string? entityId = null,
        object? oldValues = null,
        object? newValues = null,
        bool saveImmediately = true,
        CancellationToken cancellationToken = default)
    {
        var principal = _httpContextAccessor.HttpContext?.User;

        var userId = principal?.FindFirst("sub")?.Value;

        var userName =
            principal?.FindFirst("email")?.Value
            ?? principal?.Identity?.Name
            ?? "System";

        var log = new AuditLog
        {
            UserId = userId,
            UserName = userName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = oldValues is null
                ? null
                : JsonSerializer.Serialize(oldValues),
            NewValues = newValues is null
                ? null
                : JsonSerializer.Serialize(newValues),
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.AuditLogs.Add(log);

        if (saveImmediately)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}

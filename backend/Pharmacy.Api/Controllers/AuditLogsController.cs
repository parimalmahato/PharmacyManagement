
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Authorization;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Api.Controllers;

/// <summary>
/// Exposes administrator audit-history searches for the active branch.
/// </summary>
[ApiController]
[Route("api/audit-logs")]
[Authorize(Policy = ApplicationPolicies.ManageUsers)]
public sealed class AuditLogsController : ControllerBase
{
    private readonly PharmacyDbContext _db;

    public AuditLogsController(PharmacyDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Returns filtered audit history for the active branch to an authorized administrator.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? action,
        [FromQuery] string? entityType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.AuditLogs
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(x => x.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(
                x => x.EntityType == entityType);
        }

        var totalCount =
            await query.CountAsync(cancellationToken);

        var records = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.UserId,
                x.UserName,
                x.Action,
                x.EntityType,
                x.EntityId,
                x.OldValues,
                x.NewValues,
                x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            totalCount,
            page,
            pageSize,
            items = records
        });
    }
}

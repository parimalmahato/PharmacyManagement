using Microsoft.AspNetCore.Http;
using Pharmacy.Application.Branches;

namespace Pharmacy.Infrastructure.Identity;

/// <summary>
/// Resolves the validated request&apos;s branch claim or an explicit background-processing branch for branch-filtered persistence.
/// </summary>
public sealed class BranchContext(IHttpContextAccessor accessor) : IBranchContext
{
    private int? backgroundBranch;
    /// <summary>
    /// Explicit background branch when set, otherwise the request branch claim with the existing default fallback.
    /// </summary>
    public int BranchId => backgroundBranch ??
        (int.TryParse(accessor.HttpContext?.User.FindFirst("branch_id")?.Value, out var id) && id > 0 ? id : 1);
    /// <summary>
    /// Sets an explicit positive branch identifier for a scoped background operation instead of relying on request claims.
    /// </summary>
    public void UseBackgroundBranch(int id) => backgroundBranch = id > 0 ? id : throw new ArgumentException("Invalid branch.");
}

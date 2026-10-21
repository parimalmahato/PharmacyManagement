using Microsoft.AspNetCore.Http;
using Pharmacy.Application.Returns;

namespace Pharmacy.Infrastructure.Sales;

/// <summary>
/// Resolves return-inspection identity and authorization from the current request rather than trusting supplied inspector names.
/// </summary>
public sealed class ReturnInspectionContext(IHttpContextAccessor accessor) : IReturnInspectionContext
{
    public string? ApproverId
    {
        get
        {
            var user = accessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true || !(user.IsInRole("Admin") || user.IsInRole("Cashier"))) return null;
            return user.FindFirst("sub")?.Value;
        }
    }
}

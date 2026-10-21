using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Pharmacy.Infrastructure.Sales;

namespace Pharmacy.Tests.Sales;

public sealed class ReturnInspectionContextTests
{
    [Theory]
    [InlineData("Admin", "staff-123")]
    [InlineData("Cashier", "staff-123")]
    [InlineData("Pharmacist", null)]
    public void OnlyAuthenticatedReturnOperatorsCanApprove(string role, string? expected)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "staff-123"), new Claim("role", role)], "Test", "name", "role"));
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };
        Assert.Equal(expected, new ReturnInspectionContext(accessor).ApproverId);
    }

    [Fact]
    public void MissingAuthenticationOrSubjectCannotApprove()
    {
        var context = new DefaultHttpContext();
        var inspector = new ReturnInspectionContext(new HttpContextAccessor { HttpContext = context });
        Assert.Null(inspector.ApproverId);
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("role", "Admin")], "Test", "name", "role"));
        Assert.Null(inspector.ApproverId);
    }
}

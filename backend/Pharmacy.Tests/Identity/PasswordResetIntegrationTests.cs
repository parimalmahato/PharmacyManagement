using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharmacy.Api.Controllers;
using Pharmacy.Application.Auditing;
using Pharmacy.Application.Users;
using Pharmacy.Infrastructure.Auditing;
using Pharmacy.Infrastructure.Identity;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.IntegrationTests;

namespace Pharmacy.Tests.Identity;

public sealed class PasswordResetIntegrationTests
{
    [Theory]
    [InlineData("Cashier", false)]
    [InlineData("Pharmacist", false)]
    [InlineData("Cashier", true)]
    [InlineData("Pharmacist", true)]
    public async Task Reset_ChangesPasswordAndStampAndRequiresChange_OrRollsBackOnAuditFailure(string role, bool failAudit)
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddScoped(_ => database.NewContext());
        services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<PharmacyDbContext>().AddDefaultTokenProviders();
        await using var provider = services.BuildServiceProvider();
        string userId, oldStamp;
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PharmacyDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            Assert.True((await roles.CreateAsync(new IdentityRole(role))).Succeeded);
            var employee = new ApplicationUser { UserName = "employee@example.test", Email = "employee@example.test", FullName = "Test employee" };
            Assert.True((await users.CreateAsync(employee, "OriginalTest#124")).Succeeded);
            Assert.True((await users.AddToRoleAsync(employee, role)).Succeeded);
            userId = employee.Id; oldStamp = employee.SecurityStamp!;
            IAuditService audit = failAudit ? new FailingAudit() : new AuditService(db, new HttpContextAccessor());
            var controller = new UsersController(users, audit, db);
            if (failAudit)
                await Assert.ThrowsAsync<InvalidOperationException>(() => controller.ResetPassword(userId, new ResetEmployeePasswordRequest("TemporaryTest#456")));
            else
                Assert.IsType<NoContentResult>(await controller.ResetPassword(userId, new ResetEmployeePasswordRequest("TemporaryTest#456")));
        }
        using var verifyScope = provider.CreateScope();
        var verifyUsers = verifyScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var saved = (await verifyUsers.FindByIdAsync(userId))!;
        Assert.Equal(!failAudit, saved.MustChangePassword);
        Assert.Equal(failAudit, await verifyUsers.CheckPasswordAsync(saved, "OriginalTest#124"));
        Assert.Equal(!failAudit, await verifyUsers.CheckPasswordAsync(saved, "TemporaryTest#456"));
        Assert.Equal(failAudit, saved.SecurityStamp == oldStamp);
        var verify = verifyScope.ServiceProvider.GetRequiredService<PharmacyDbContext>();
        Assert.Equal(failAudit ? 0 : 1, await verify.AuditLogs.CountAsync(x => x.Action == "EmployeePasswordReset"));
    }

    private sealed class FailingAudit : IAuditService
    {
        public Task WriteAsync(string action, string entityType, string? entityId = null,
            object? oldValues = null, object? newValues = null, bool saveImmediately = true,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("Simulated audit failure");
    }
}

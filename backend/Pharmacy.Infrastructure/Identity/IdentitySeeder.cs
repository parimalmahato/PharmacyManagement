
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Pharmacy.Infrastructure.Identity;

/// <summary>
/// Bootstraps required identity roles and the configured initial administrator rather than exposing public account registration.
/// </summary>
public static class IdentitySeeder
{
    private static readonly string[] Roles =
    {
        "Admin",
        "Pharmacist",
        "Cashier"
    };

    /// <summary>
    /// Creates required roles and the configured initial administrator using deployment settings rather than hard-coded shared credentials.
    /// </summary>
    public static async Task SeedAsync(
        IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();

        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole>>();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var configuration = scope.ServiceProvider
            .GetRequiredService<IConfiguration>();

        var logger = scope.ServiceProvider
            .GetRequiredService<ILogger<ApplicationUser>>();

        // Create roles if they do not exist.
        foreach (var roleName in Roles)
        {
            if (await roleManager.RoleExistsAsync(roleName))
                continue;

            var roleResult = await roleManager.CreateAsync(
                new IdentityRole(roleName));

            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Unable to create role {roleName}: " +
                    string.Join(", ",
                        roleResult.Errors.Select(e => e.Description)));
            }

            logger.LogInformation(
                "Created Identity role: {RoleName}",
                roleName);
        }

        // Initial administrator credentials come from configuration.
        var adminEmail = configuration["SeedAdmin:Email"];
        var adminPassword = configuration["SeedAdmin:Password"];
        var adminFullName = configuration["SeedAdmin:FullName"];

        // Skip admin creation when no bootstrap credentials exist.
        if (string.IsNullOrWhiteSpace(adminEmail) ||
            string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogInformation(
                "Admin bootstrap credentials not configured. " +
                "Skipping initial admin creation.");

            return;
        }

        adminEmail = adminEmail.Trim();

        var adminUser = await userManager.FindByEmailAsync(
            adminEmail);

        if (adminUser is null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = string.IsNullOrWhiteSpace(adminFullName)
                    ? "System Administrator"
                    : adminFullName.Trim(),
                EmailConfirmed = true,
                IsActive = true,
                MustChangePassword = true
            };

            var createResult = await userManager.CreateAsync(
                adminUser,
                adminPassword);

            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Unable to create initial administrator: " +
                    string.Join(", ",
                        createResult.Errors.Select(e => e.Description)));
            }

            logger.LogInformation(
                "Initial administrator created successfully.");
        }

        // Assign Admin role if missing.
        if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
        {
            var assignResult = await userManager.AddToRoleAsync(
                adminUser,
                "Admin");

            if (!assignResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Unable to assign Admin role: " +
                    string.Join(", ",
                        assignResult.Errors.Select(e => e.Description)));
            }
        }
    }
}

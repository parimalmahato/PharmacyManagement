using Pharmacy.Application;
using Pharmacy.Infrastructure;
using Pharmacy.Api.BackgroundServices;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Pharmacy.Infrastructure.Identity;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Application.Authorization;
using Pharmacy.Application.Auditing;
using Pharmacy.Infrastructure.Auditing;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<Pharmacy.Api.Infrastructure.ApiExceptionHandler>();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    foreach (var address in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
    {
        if (!System.Net.IPAddress.TryParse(address, out var ip)) throw new InvalidOperationException("ReverseProxy:KnownProxies must contain IP addresses.");
        options.KnownProxies.Add(ip);
    }
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("help-assistant", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst("sub")?.Value ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(new { message = "Too many requests. Try again in a minute." }, ct);
    };
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactFrontend", policy =>
        policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ??
            (builder.Environment.IsDevelopment() ? ["http://localhost:5173"] : Array.Empty<string>()))
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<ExpiryNotificationWorker>();
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;

        options.User.RequireUniqueEmail = true;

        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(15);

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<PharmacyDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException(
        "JWT issuer is missing.");

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException(
        "JWT audience is missing.");

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "JWT signing key is missing.");

if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "JWT signing key must be at least 32 bytes.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,

                ValidateAudience = true,
                ValidAudience = jwtAudience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey)),

                ValidateLifetime = true,
                RequireExpirationTime = true,

                ClockSkew = TimeSpan.FromSeconds(30),

                RoleClaimType = "role",
                NameClaimType = "name"
            };
            
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var userManager =
                        context.HttpContext.RequestServices
                            .GetRequiredService<
                                UserManager<ApplicationUser>>();

                    var userId = context.Principal?
                        .FindFirst("sub")?.Value;

                    var tokenSecurityStamp = context.Principal?
                        .FindFirst("security_stamp")?.Value;

                    if (string.IsNullOrWhiteSpace(userId) ||
                        string.IsNullOrWhiteSpace(tokenSecurityStamp))
                    {
                        context.Fail("Invalid token.");
                        return;
                    }

                    var user = await userManager.FindByIdAsync(userId);

                    if (user is null || !user.IsActive)
                    {
                        context.Fail("Account is inactive or missing.");
                        return;
                    }

                    var currentStamp =
                        await userManager.GetSecurityStampAsync(user);
                    var branchClaim = context.Principal?.FindFirst("branch_id")?.Value;
                    var branchDb = context.HttpContext.RequestServices.GetRequiredService<PharmacyDbContext>();
                    if (!int.TryParse(branchClaim, out var activeBranch) || activeBranch <= 0 ||
                        (!context.Principal!.IsInRole("Admin") && activeBranch != user.BranchId) ||
                        !await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(branchDb.Branches, x => x.Id == activeBranch && x.IsActive))
                    { context.Fail("Branch access is invalid."); return; }

                    if (currentStamp != tokenSecurityStamp)
                    {
                        context.Fail("Session has been invalidated.");
                        return;
                    }

                    if (user.MustChangePassword)
                    {
                        var path = context.HttpContext.Request.Path;

                        if (!path.StartsWithSegments(
                                "/api/auth/change-password"))
                        {
                            context.Fail("Password change required.");
                        }
                    }
                }
            };

    });


builder.Services.AddAuthorization(options =>
{
    // Require authentication for endpoints by default.
    options.FallbackPolicy =
        new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();

    options.AddPolicy(
        ApplicationPolicies.ManageMedicines,
        policy => policy.RequireRole(
            ApplicationRoles.Admin,
            ApplicationRoles.Pharmacist));

    options.AddPolicy(
        ApplicationPolicies.ViewMedicines,
        policy => policy.RequireRole(
            ApplicationRoles.Admin,
            ApplicationRoles.Pharmacist,
            ApplicationRoles.Cashier));

    options.AddPolicy(
        ApplicationPolicies.ManageInventory,
        policy => policy.RequireRole(
            ApplicationRoles.Admin,
            ApplicationRoles.Pharmacist));

    options.AddPolicy(
        ApplicationPolicies.ViewInventory,
        policy => policy.RequireRole(
            ApplicationRoles.Admin,
            ApplicationRoles.Pharmacist,
            ApplicationRoles.Cashier));

    options.AddPolicy(
        ApplicationPolicies.ManagePurchasing,
        policy => policy.RequireRole(
            ApplicationRoles.Admin,
            ApplicationRoles.Pharmacist));

    options.AddPolicy(
        ApplicationPolicies.CreateSales,
        policy => policy.RequireRole(
            ApplicationRoles.Admin,
            ApplicationRoles.Cashier));

    options.AddPolicy(
        ApplicationPolicies.ViewSales,
        policy => policy.RequireRole(
            ApplicationRoles.Admin,
            ApplicationRoles.Pharmacist,
            ApplicationRoles.Cashier));

    options.AddPolicy(
        ApplicationPolicies.ManageCustomers,
        policy => policy.RequireRole(
            ApplicationRoles.Admin,
            ApplicationRoles.Cashier));

    options.AddPolicy(
        ApplicationPolicies.ViewReports,
        policy => policy.RequireRole(
            ApplicationRoles.Admin,
            ApplicationRoles.Pharmacist));

    options.AddPolicy(
        ApplicationPolicies.ViewExpiry,
        policy => policy.RequireRole(
            ApplicationRoles.Admin,
            ApplicationRoles.Pharmacist));

    options.AddPolicy(
        ApplicationPolicies.ManageNotifications,
        policy => policy.RequireRole(
            ApplicationRoles.Admin,
            ApplicationRoles.Pharmacist));

    options.AddPolicy(
        ApplicationPolicies.ManageUsers,
        policy => policy.RequireRole(
            ApplicationRoles.Admin));
    options.AddPolicy(
        ApplicationPolicies.ViewCustomers,
        policy => policy.RequireRole(
        ApplicationRoles.Admin,
        ApplicationRoles.Pharmacist,
        ApplicationRoles.Cashier));
    options.AddPolicy(
    ApplicationPolicies.ManagePharmacySettings,
    policy => policy
        .RequireAuthenticatedUser()
        .RequireRole("Admin"));
});
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IAuditService, AuditService>();

var app = builder.Build();
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers.CacheControl = "no-store";
    await next();
});
if (!app.Environment.IsDevelopment()) { app.UseHsts(); app.UseHttpsRedirection(); }

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("BootstrapIdentity:Enabled"))
    await IdentitySeeder.SeedAsync(app.Services);

app.UseCors("ReactFrontend");

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "Healthy",
    application = "Pharmacy Management API",
    timestampUtc = DateTime.UtcNow
})).AllowAnonymous();
app.MapGet("/api/health/ready", async (PharmacyDbContext db, CancellationToken ct) =>
{
    try
    {
        if (!await db.Database.CanConnectAsync(ct)) return Results.StatusCode(503);
        if ((await db.Database.GetPendingMigrationsAsync(ct)).Any()) return Results.StatusCode(503);
        return Results.Ok(new { status = "Ready" });
    }
    catch { return Results.StatusCode(503); }
}).AllowAnonymous();

app.Run();

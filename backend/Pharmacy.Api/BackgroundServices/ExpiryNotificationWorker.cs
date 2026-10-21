
using Pharmacy.Infrastructure.Notifications;

namespace Pharmacy.Api.BackgroundServices;

/// <summary>
/// Periodically creates expiry notifications using fresh scoped services and explicit branch processing.
/// </summary>
public sealed class ExpiryNotificationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiryNotificationWorker> _logger;

    public ExpiryNotificationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ExpiryNotificationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Runs periodic notification processing in scoped services and respects host shutdown cancellation.
    /// </summary>
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Expiry notification worker started.");

        // Run immediately on application startup.
        await RunNotificationCheckAsync(stoppingToken);

        // Re-read saved configuration on each check.
        using var timer = new PeriodicTimer(
            TimeSpan.FromMinutes(15));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunNotificationCheckAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Expiry notification worker stopped.");
        }
    }

    private async Task RunNotificationCheckAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            using var directoryScope = _scopeFactory.CreateScope();
            var directory = directoryScope.ServiceProvider.GetRequiredService<Pharmacy.Infrastructure.Persistence.PharmacyDbContext>();
            var branchIds = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                directory.Branches.Where(x => x.IsActive).Select(x => x.Id), cancellationToken);
            foreach (var branchId in branchIds)
            {
                using var scope = _scopeFactory.CreateScope();
                scope.ServiceProvider.GetRequiredService<Pharmacy.Infrastructure.Identity.BranchContext>().UseBackgroundBranch(branchId);
                await scope.ServiceProvider.GetRequiredService<ExpiryNotificationProcessor>().ProcessAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing medicine expiry notifications.");
        }
    }
}

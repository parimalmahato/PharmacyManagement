using Microsoft.EntityFrameworkCore;
using Pharmacy.Infrastructure.Persistence;

// This executable is an explicit deployment step, not an API-startup side effect.
// Supply the target database through the environment; no development settings are read.
var connection = Environment.GetEnvironmentVariable("ConnectionStrings__PharmacyDb");
if (string.IsNullOrWhiteSpace(connection))
    throw new InvalidOperationException("Set ConnectionStrings__PharmacyDb before running migrations.");

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
var options = new DbContextOptionsBuilder<PharmacyDbContext>().UseSqlServer(connection).Options;
await using var db = new PharmacyDbContext(options);
Console.WriteLine("Applying pending Pharmacy migrations to the configured database.");
await db.Database.MigrateAsync(cancellation.Token);
Console.WriteLine("Pharmacy database migrations completed.");

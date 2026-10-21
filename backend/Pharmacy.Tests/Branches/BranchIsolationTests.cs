using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Branches;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Tests.Branches;

public sealed class BranchIsolationTests
{
    public sealed class Context(int id) : IBranchContext { public int BranchId => id; }
    private static DbContextOptions<PharmacyDbContext> Options() => new DbContextOptionsBuilder<PharmacyDbContext>()
        .UseSqlServer("Server=unused;Database=unused;Integrated Security=True;TrustServerCertificate=True").Options;
    [Fact]
    public void BranchFiltersUseTheCurrentContext_NotTheFirstCachedBranch()
    {
        var options = Options();
        using var main = new PharmacyDbContext(options, new Context(1));
        using var other = new PharmacyDbContext(options, new Context(2));
        Assert.Contains("= 1", main.MedicineBatches.ToQueryString());
        Assert.Contains("= 2", other.MedicineBatches.ToQueryString());
        Assert.Contains("BranchId", other.Payments.ToQueryString());
        Assert.Contains("BranchId", other.SaleReturnItems.ToQueryString());
        Assert.Contains("BranchId", other.PharmacySettings.ToQueryString());
        Assert.DoesNotContain("BranchId", other.Medicines.ToQueryString());
    }
    [Fact]
    public async Task CrossBranchUpdatesAreRejectedBeforeAnyDatabaseConnection()
    {
        await using var db = new PharmacyDbContext(Options(), new Context(1));
        var batch = new MedicineBatch(1, "TEST", Pharmacy.Domain.BusinessClock.Today.AddYears(1), 10, 20, 1);
        db.Attach(batch); db.Entry(batch).Property(x => x.Id).CurrentValue = 123;
        db.Entry(batch).Property(x => x.Id).IsTemporary = false;
        db.Entry(batch).Property("BranchId").CurrentValue = 2;
        db.Entry(batch).State = EntityState.Modified;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
}

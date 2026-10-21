using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Sales;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Auditing;
using Pharmacy.Infrastructure.Sales;
using Xunit;

namespace Pharmacy.IntegrationTests;

public sealed class SaleRepositoryIntegrationTests
{
    [Fact]
    public async Task RetriedCheckoutDoesNotCreateAnotherInvoiceOrDeductStockTwice()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        int medicineId;
        await using (var setup = database.NewContext()) medicineId = await SeedMedicineAsync(setup, ("RETRY", 10, 30, 105m));
        var request = new CreateSaleRequest([new(medicineId, 1)], null, Guid.NewGuid());
        await using var db = database.NewContext();
        var first = await Repository(db).CreateAsync(request);
        var retry = await Repository(db).CreateAsync(request);
        Assert.Equal(first.Id, retry.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Repository(db).CreateAsync(request with { Items = [new(medicineId, 2)] }));
        Assert.Equal(1, await db.Sales.CountAsync());
        Assert.Equal(9, (await db.MedicineBatches.SingleAsync()).Quantity);
        Assert.Equal(1, await db.StockMovements.CountAsync());
    }
    private static SaleRepository Repository(Pharmacy.Infrastructure.Persistence.PharmacyDbContext db) =>
        new(db, new AuditService(db, new HttpContextAccessor()));

    private static async Task<int> SeedMedicineAsync(
        Pharmacy.Infrastructure.Persistence.PharmacyDbContext db,
        params (string Batch, int Quantity, int DaysUntilExpiry, decimal Price)[] batches)
    {
        var medicine = new Medicine("Paracetamol 500mg", "Paracetamol", "Test Manufacturer", "Tablets");
        medicine.ConfigureTax("3004", 5m);
        db.Medicines.Add(medicine);
        await db.SaveChangesAsync();
        foreach (var b in batches)
            db.MedicineBatches.Add(new MedicineBatch(
                medicine.Id, b.Batch, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(b.DaysUntilExpiry),
                50m, b.Price, b.Quantity));
        await db.SaveChangesAsync();
        return medicine.Id;
    }

    [Fact]
    public async Task Checkout_UsesFefoAcrossBatches_AndPersistsTaxAndMovements()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        int medicineId;
        await using (var setup = database.NewContext())
            medicineId = await SeedMedicineAsync(setup, ("EARLY", 5, 30, 105m), ("LATER", 10, 90, 210m));

        SaleDto sale;
        await using (var db = database.NewContext())
            sale = await Repository(db).CreateAsync(new CreateSaleRequest(
                new List<CreateSaleItemRequest> { new(medicineId, 8, 10m) }, null));

        Assert.Equal(2, sale.Items.Count);
        Assert.Equal(8, sale.Items.Sum(x => x.Quantity));
        Assert.Equal(5, sale.Items.Single(x => x.BatchNumber == "EARLY").Quantity);
        Assert.Equal(3, sale.Items.Single(x => x.BatchNumber == "LATER").Quantity);
        Assert.Equal(1155m, sale.SubtotalAmount); // 5*105 + 3*210
        Assert.Equal(115.50m, sale.DiscountAmount);
        Assert.Equal(1039.50m, sale.TotalAmount);
        Assert.Equal(sale.TotalAmount, sale.TaxableAmount + sale.GstAmount);
        Assert.Equal(sale.TotalAmount, sale.Items.Sum(x => x.LineTotal));
        Assert.All(sale.Items, item => Assert.Equal("3004", item.HsnCode));

        await using var verify = database.NewContext();
        var stock = await verify.MedicineBatches.ToDictionaryAsync(x => x.BatchNumber, x => x.Quantity);
        Assert.Equal(0, stock["EARLY"]);
        Assert.Equal(7, stock["LATER"]);
        var movements = await verify.StockMovements.Where(x => x.MovementType == StockMovementType.Sale).ToListAsync();
        Assert.Equal(2, movements.Count);
        Assert.Equal(-8, movements.Sum(x => x.QuantityChange));
        Assert.Single(await verify.AuditLogs.Where(x => x.Action == "SaleCompleted").ToListAsync());
    }

    [Fact]
    public async Task InsufficientStock_RollsBackSaleAndAuditAndStock()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        int medicineId;
        await using (var setup = database.NewContext())
            medicineId = await SeedMedicineAsync(setup, ("ONLY", 2, 30, 105m));

        await using (var db = database.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(() => Repository(db).CreateAsync(
                new CreateSaleRequest(new List<CreateSaleItemRequest> { new(medicineId, 3, 0m) }, null)));

        await using var verify = database.NewContext();
        Assert.Equal(2, await verify.MedicineBatches.Select(x => x.Quantity).SingleAsync());
        Assert.Equal(0, await verify.Sales.CountAsync());
        Assert.Equal(0, await verify.SaleItems.CountAsync());
        Assert.Equal(0, await verify.StockMovements.CountAsync());
        Assert.Equal(0, await verify.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task TwoSimultaneousCheckouts_CannotBothSellTheLastUnits()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        int medicineId;
        await using (var setup = database.NewContext())
            medicineId = await SeedMedicineAsync(setup, ("LAST", 5, 30, 105m));

        async Task<bool> AttemptAsync()
        {
            await using var db = database.NewContext();
            try
            {
                await Repository(db).CreateAsync(new CreateSaleRequest(
                    new List<CreateSaleItemRequest> { new(medicineId, 5, 0m) }, null));
                return true;
            }
            catch (Exception ex) when (ex is InvalidOperationException or Microsoft.Data.SqlClient.SqlException)
            {
                // May fail due to insufficient stock or SQL Server deadlock victim.
                return false;
            }
        }

        var results = await Task.WhenAll(Task.Run(AttemptAsync), Task.Run(AttemptAsync));
        Assert.Equal(1, results.Count(success => success));
        await using var verify = database.NewContext();
        Assert.Equal(0, await verify.MedicineBatches.Select(x => x.Quantity).SingleAsync());
        Assert.Equal(1, await verify.Sales.CountAsync());
        Assert.Equal(-5, await verify.StockMovements.SumAsync(x => x.QuantityChange));
        Assert.Equal(1, await verify.AuditLogs.CountAsync(x => x.Action == "SaleCompleted"));
    }
}

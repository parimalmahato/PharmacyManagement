using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Payments.DTOs;
using Pharmacy.Domain.Entities;
using Pharmacy.Domain.Entities.Payments;
using Pharmacy.Domain.Entities.Sales;
using Pharmacy.Infrastructure.Auditing;
using Pharmacy.Infrastructure.Payments;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.IntegrationTests;

namespace Pharmacy.Tests.Payments;

public sealed class PaymentRepositoryIntegrationTests
{
    private static PaymentRepository Repository(PharmacyDbContext db) =>
        new(db, new AuditService(db, new HttpContextAccessor()));

    private static async Task<int> SeedAsync(SqlServerTestDatabase database)
    {
        await using var db = database.NewContext();
        var medicine = new Medicine("Test", "Generic", "Manufacturer", "Tablets");
        db.Medicines.Add(medicine);
        await db.SaveChangesAsync();
        var batch = new MedicineBatch(medicine.Id, "PAY", DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1), 50m, 100m, 10);
        db.MedicineBatches.Add(batch);
        await db.SaveChangesAsync();
        var sale = new Sale(Guid.NewGuid().ToString());
        sale.AddItem(new SaleItem(medicine.Id, batch.Id, 1, 100m, "Test", null, 0m));
        db.Sales.Add(sale);
        await db.SaveChangesAsync();
        return sale.Id;
    }

    [Fact]
    public async Task PartialPayment_Retry_Conflict_AndSettlement()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        var saleId = await SeedAsync(database);
        var request = new RecordPaymentRequestDto(PaymentMethod.Cash, 40m, null, Guid.NewGuid());
        await using var db = database.NewContext();
        var repository = Repository(db);
        var receipt = await repository.RecordAsync(saleId, request);
        Assert.Equal(receipt, await repository.RecordAsync(saleId, request));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.RecordAsync(saleId, request with { Amount = 41m }));
        var partial = await repository.GetBySaleAsync(saleId);
        Assert.Equal("PartiallyPaid", partial!.PaymentStatus);
        Assert.Equal(60m, partial.BalanceAmount);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.RecordAsync(saleId, request with { Amount = 61m, IdempotencyKey = Guid.NewGuid() }));
        await repository.RecordAsync(saleId, new(PaymentMethod.Upi, 60m, "UPI123", Guid.NewGuid()));
        Assert.Equal("Paid", (await repository.GetBySaleAsync(saleId))!.PaymentStatus);
        Assert.Equal(receipt, await repository.RecordAsync(saleId, request));
        Assert.Equal(2, await db.Payments.CountAsync());
        Assert.Equal(2, await db.AuditLogs.CountAsync(x => x.Action == "PaymentRecorded"));
    }

    [Fact]
    public async Task ConcurrentPayments_CannotOverpay()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        var saleId = await SeedAsync(database);
        async Task<bool> Pay()
        {
            await using var db = database.NewContext();
            try
            {
                await Repository(db).RecordAsync(saleId, new(PaymentMethod.Cash, 60m, null, Guid.NewGuid()));
                return true;
            }
            catch (InvalidOperationException) { return false; }
        }
        var results = await Task.WhenAll(Pay(), Pay());
        Assert.Single(results, x => x);
        await using var verify = database.NewContext();
        Assert.Equal(60m, await verify.Payments.SumAsync(x => x.Amount));
    }
}

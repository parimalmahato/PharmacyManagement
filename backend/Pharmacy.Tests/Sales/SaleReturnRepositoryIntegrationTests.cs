using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Auditing;
using Pharmacy.Application.Payments.DTOs;
using Pharmacy.Application.Returns;
using Pharmacy.Domain.Entities;
using Pharmacy.Domain.Entities.Payments;
using Pharmacy.Domain.Entities.Sales;
using Pharmacy.Domain.Entities.Sales.Returns;
using Pharmacy.Infrastructure.Auditing;
using Pharmacy.Infrastructure.Payments;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Sales;
using Pharmacy.IntegrationTests;

namespace Pharmacy.Tests.Sales;

public sealed class SaleReturnRepositoryIntegrationTests
{
    private sealed class TestInspector : IReturnInspectionContext { public string ApproverId => "test-cashier"; }

    [Fact]
    public async Task InspectionIdentityIsRequiredAndRecorded()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        var seed = await SeedAsync(database);
        var request = new CreateReturnRequest("Inspected package", Guid.NewGuid(), [new(seed.ItemId, 1, true, ReturnDisposition.Saleable, true)]);
        await using (var unauthenticated = database.NewContext())
            await Assert.ThrowsAsync<ArgumentException>(() => new SaleReturnRepository(unauthenticated, Audit(unauthenticated)).CreateAsync(seed.SaleId, request));
        await using var db = database.NewContext();
        var result = await Repository(db).CreateAsync(seed.SaleId, request);
        var line = Assert.Single(result.Items);
        Assert.True(line.InspectionApproved);
        Assert.Equal("test-cashier", line.InspectedByUserId);
        Assert.NotNull(line.InspectedAtUtc);
        Assert.Equal(result.Id, (await Repository(db).CreateAsync(seed.SaleId, request)).Id);
        Assert.Equal(8, (await db.MedicineBatches.SingleAsync()).Quantity);
        Assert.Single(await db.StockMovements.ToListAsync());
    }
    private static IAuditService Audit(PharmacyDbContext db) => new AuditService(db, new HttpContextAccessor());
    private static SaleReturnRepository Repository(PharmacyDbContext db) => new(db, Audit(db), new TestInspector());

    private static async Task<(int SaleId, int ItemId, int BatchId)> SeedAsync(SqlServerTestDatabase database, bool paid = false)
    {
        await using var db = database.NewContext();
        var medicine = new Medicine("Return test", "Generic", "Manufacturer", "Tablet");
        db.Medicines.Add(medicine); await db.SaveChangesAsync();
        var batch = new MedicineBatch(medicine.Id, "RET", DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1), 5, 10, 7);
        db.MedicineBatches.Add(batch); await db.SaveChangesAsync();
        var sale = new Sale(Guid.NewGuid().ToString());
        sale.AddItem(new SaleItem(medicine.Id, batch.Id, 3, 10, medicine.Name, "3004", 5, 10));
        db.Sales.Add(sale); await db.SaveChangesAsync();
        if (paid) await new PaymentRepository(db, Audit(db)).RecordAsync(sale.Id, new(PaymentMethod.Cash, 27, null, Guid.NewGuid()));
        return (sale.Id, sale.Items.Single().Id, batch.Id);
    }

    [Fact]
    public async Task PaidReturn_RestocksOnce_RefundsOnce_AndPreservesInvoice()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        var seed = await SeedAsync(database, true);
        var request = new CreateReturnRequest("Unopened package", Guid.NewGuid(), [new(seed.ItemId, 1, true, ReturnDisposition.Saleable, true)]);
        await using var db = database.NewContext();
        var repository = Repository(db);
        var returned = await repository.CreateAsync(seed.SaleId, request);
        Assert.Equal(9m, returned.TotalAmount);
        Assert.Equal(returned.Id, (await repository.CreateAsync(seed.SaleId, request)).Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.CreateAsync(seed.SaleId, request with { Reason = "Different" }));
        var refund = new RefundRequest(RefundMethod.Cash, 4, null, Guid.NewGuid());
        var receipt = await repository.RefundAsync(seed.SaleId, refund);
        Assert.Equal(receipt, await repository.RefundAsync(seed.SaleId, refund));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.RefundAsync(seed.SaleId, refund with { Amount = 6, IdempotencyKey = Guid.NewGuid() }));
        await repository.RefundAsync(seed.SaleId, new(RefundMethod.Upi, 5, "refund-reference", Guid.NewGuid()));
        Assert.Equal(0m, (await repository.GetAsync(seed.SaleId))!.RefundDue);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new PaymentRepository(db, Audit(db)).RecordAsync(seed.SaleId, new(PaymentMethod.Cash, 1, null, Guid.NewGuid())));
        await using var verify = database.NewContext();
        var sale = await verify.Sales.Include(x => x.Items).SingleAsync();
        Assert.Equal(27m, sale.TotalAmount);
        Assert.Equal(3, sale.Items.Single().Quantity);
        Assert.Single(await verify.Payments.ToListAsync());
        Assert.Equal(8, (await verify.MedicineBatches.SingleAsync()).Quantity);
        var movement = await verify.StockMovements.SingleAsync();
        Assert.Equal(StockMovementType.SalesReturn, movement.MovementType);
        Assert.Equal(1, movement.QuantityChange);
        Assert.Equal(1, await verify.AuditLogs.CountAsync(x => x.Action == "SaleReturned"));
        Assert.Equal(2, await verify.AuditLogs.CountAsync(x => x.Action == "SaleRefunded"));
    }

    [Fact]
    public async Task UnpaidReturn_ReducesBalance_WithoutRefundOrStockAddition()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        var seed = await SeedAsync(database);
        await using var db = database.NewContext();
        var repository = Repository(db);
        await repository.CreateAsync(seed.SaleId, new("Damaged", Guid.NewGuid(), [new(seed.ItemId, 1, false)]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.RefundAsync(seed.SaleId, new(RefundMethod.Cash, 1, null, Guid.NewGuid())));
        var payments = new PaymentRepository(db, Audit(db));
        Assert.Equal(18m, (await payments.GetBySaleAsync(seed.SaleId))!.BalanceAmount);
        await payments.RecordAsync(seed.SaleId, new(PaymentMethod.Cash, 18, null, Guid.NewGuid()));
        Assert.Equal("Paid", (await payments.GetBySaleAsync(seed.SaleId))!.PaymentStatus);
        Assert.Equal(7, (await db.MedicineBatches.SingleAsync()).Quantity);
        Assert.Empty(await db.StockMovements.ToListAsync());
    }

    [Fact]
    public async Task ExpiredReturns_CannotEnterSaleableInventory()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        var seed = await SeedAsync(database);
        await using var db = database.NewContext();
        await db.MedicineBatches.ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ExpiryDate, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1)));
        var repository = Repository(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.CreateAsync(seed.SaleId, new("Expired", Guid.NewGuid(), [new(seed.ItemId, 1, true, ReturnDisposition.Saleable, true)])));
        await repository.CreateAsync(seed.SaleId, new("Expired, segregated", Guid.NewGuid(), [new(seed.ItemId, 1, false)]));
        Assert.Equal(7, (await db.MedicineBatches.SingleAsync()).Quantity);
    }

    [Fact]
    public async Task ConcurrentReturns_CannotExceedSoldQuantity()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        var seed = await SeedAsync(database);
        async Task<bool> Return()
        {
            await using var db = database.NewContext();
            try { await Repository(db).CreateAsync(seed.SaleId, new("Unopened", Guid.NewGuid(), [new(seed.ItemId, 2, true, ReturnDisposition.Saleable, true)])); return true; }
            catch (ArgumentException) { return false; }
        }
        Assert.Single(await Task.WhenAll(Return(), Return()), x => x);
        await using var verify = database.NewContext();
        Assert.Equal(2, await verify.SaleReturnItems.SumAsync(x => x.Quantity));
        Assert.Equal(9, (await verify.MedicineBatches.SingleAsync()).Quantity);
    }

    [Fact]
    public async Task ConcurrentRefunds_CannotExceedRefundDue()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        var seed = await SeedAsync(database, true);
        await using (var setup = database.NewContext())
            await Repository(setup).CreateAsync(seed.SaleId, new("Unopened", Guid.NewGuid(), [new(seed.ItemId, 1, false)]));
        async Task<bool> Refund()
        {
            await using var db = database.NewContext();
            try { await Repository(db).RefundAsync(seed.SaleId, new(RefundMethod.Cash, 6, null, Guid.NewGuid())); return true; }
            catch (InvalidOperationException) { return false; }
        }
        Assert.Single(await Task.WhenAll(Refund(), Refund()), x => x);
        await using var verify = database.NewContext();
        Assert.Equal(6m, await verify.SaleRefunds.SumAsync(x => x.Amount));
    }

    [Fact]
    public async Task AuditFailure_RollsBackReturnStockAndMovements()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        var seed = await SeedAsync(database);
        await using (var db = database.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(() => new SaleReturnRepository(db, new FailingAudit(), new TestInspector()).CreateAsync(seed.SaleId,
                new("Unopened", Guid.NewGuid(), [new(seed.ItemId, 1, true, ReturnDisposition.Saleable, true)])));
        await using var verify = database.NewContext();
        Assert.Empty(await verify.SaleReturns.ToListAsync());
        Assert.Empty(await verify.SaleReturnItems.ToListAsync());
        Assert.Empty(await verify.StockMovements.ToListAsync());
        Assert.Equal(7, (await verify.MedicineBatches.SingleAsync()).Quantity);
    }

    private sealed class FailingAudit : IAuditService
    {
        public Task WriteAsync(string action, string entityType, string? entityId = null, object? oldValues = null,
            object? newValues = null, bool saveImmediately = true, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated audit failure.");
    }

    [Fact]
    public async Task AtomicReturnAndRefund_RetriesWithoutDuplicatingStockOrRefund()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        var seed = await SeedAsync(database, true);
        var request = new CreateReturnRequest("Inspected sealed", Guid.NewGuid(),
            [new(seed.ItemId, 1, true, ReturnDisposition.Saleable, true)], new(RefundMethod.Cash, 9, null));
        await using var db = database.NewContext();
        var first = await Repository(db).CreateAsync(seed.SaleId, request);
        var retry = await Repository(db).CreateAsync(seed.SaleId, request);
        Assert.Equal(first.Id, retry.Id);
        Assert.Equal(first.Refund, retry.Refund);
        Assert.Equal(first.Id, first.Refund!.SaleReturnId);
        Assert.Single(await db.SaleRefunds.ToListAsync());
        Assert.Single(await db.StockMovements.ToListAsync());
        Assert.Equal(8, (await db.MedicineBatches.SingleAsync()).Quantity);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Repository(db).CreateAsync(seed.SaleId, request with { Refund = new(RefundMethod.Cash, 8, null) }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AtomicFailure_RollsBackReturnRefundAndStock(bool failAudit)
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        var seed = await SeedAsync(database, true);
        await using (var db = database.NewContext())
        {
            var repository = new SaleReturnRepository(db, failAudit ? new RefundFailingAudit(db) : Audit(db), new TestInspector());
            await Assert.ThrowsAsync<InvalidOperationException>(() => repository.CreateAsync(seed.SaleId,
                new("Inspected", Guid.NewGuid(), [new(seed.ItemId, 1, true, ReturnDisposition.Saleable, true)], new(RefundMethod.Cash, failAudit ? 9 : 10, null))));
        }
        await using var verify = database.NewContext();
        Assert.Empty(await verify.SaleReturns.ToListAsync());
        Assert.Empty(await verify.SaleReturnItems.ToListAsync());
        Assert.Empty(await verify.SaleRefunds.ToListAsync());
        Assert.Empty(await verify.StockMovements.ToListAsync());
        Assert.Equal(7, (await verify.MedicineBatches.SingleAsync()).Quantity);
        Assert.Equal(27m, (await verify.Sales.SingleAsync()).TotalAmount);
        Assert.Empty(await verify.AuditLogs.Where(x => x.Action == "SaleReturned" || x.Action == "SaleRefunded").ToListAsync());
    }

    [Theory]
    [InlineData(ReturnDisposition.Damaged)]
    [InlineData(ReturnDisposition.Expired)]
    [InlineData(ReturnDisposition.Quarantine)]
    [InlineData(ReturnDisposition.NonResaleable)]
    public async Task UnsafeDisposition_RejectsRestockingEvenWhenBatchIsUnexpired(ReturnDisposition disposition)
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        var seed = await SeedAsync(database);
        await using (var db = database.NewContext())
            await Assert.ThrowsAsync<ArgumentException>(() => Repository(db).CreateAsync(seed.SaleId,
                new("Unsafe", Guid.NewGuid(), [new(seed.ItemId, 1, true, disposition)])));
        await using var verify = database.NewContext();
        Assert.Empty(await verify.SaleReturns.ToListAsync());
        Assert.Equal(7, (await verify.MedicineBatches.SingleAsync()).Quantity);
    }

    private sealed class RefundFailingAudit(PharmacyDbContext db) : IAuditService
    {
        public Task WriteAsync(string action, string entityType, string? entityId = null, object? oldValues = null,
            object? newValues = null, bool saveImmediately = true, CancellationToken cancellationToken = default)
        {
            if (action == "SaleRefunded") throw new InvalidOperationException("Simulated refund audit failure.");
            return Audit(db).WriteAsync(action, entityType, entityId, oldValues, newValues, saveImmediately, cancellationToken);
        }
    }
}

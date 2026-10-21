using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Auditing;
using Pharmacy.Application.Sales;
using Pharmacy.Domain;
using Pharmacy.Domain.Entities;
using Pharmacy.Domain.Entities.Payments;
using Pharmacy.Domain.Entities.Sales;
using Pharmacy.Domain.Entities.Sales.Returns;
using Pharmacy.Infrastructure.Auditing;
using Pharmacy.Infrastructure.Payments;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Reports;
using Pharmacy.Infrastructure.Sales;
using Pharmacy.IntegrationTests;

namespace Pharmacy.Tests.Sales;

public sealed class SaleCancellationIntegrationTests
{
    private static IAuditService Audit(PharmacyDbContext db) => new AuditService(db, new HttpContextAccessor());
    private static SaleRepository Repo(PharmacyDbContext db) => new(db, Audit(db));
    private static CancelSaleRequest Request(bool restock = true) => new("Incorrect billing; medicines retained", Guid.NewGuid(), true, restock, restock);
    private static async Task<int> Seed(SqlServerTestDatabase database, bool paid = false)
    {
        await using var db = database.NewContext();
        var medicine = new Medicine("Test medicine", "Generic", "Maker", "Tablet");
        db.Medicines.Add(medicine); await db.SaveChangesAsync();
        var batch = new MedicineBatch(medicine.Id, "VOID-BATCH", BusinessClock.Today.AddYears(1), 5m, 10m, 10);
        db.MedicineBatches.Add(batch); await db.SaveChangesAsync();
        batch.RemoveStock(2);
        var sale = new Sale("VOID-TEST"); sale.AddItem(new SaleItem(medicine.Id, batch.Id, 2, 10m, medicine.Name, null, 0));
        db.Sales.Add(sale); await db.SaveChangesAsync();
        db.StockMovements.Add(new StockMovement(batch.Id, StockMovementType.Sale, -2, $"Sale:{sale.Id}"));
        if (paid) db.Payments.Add(new Payment(sale.Id, PaymentMethod.Cash, 10m, null, Guid.NewGuid()));
        await db.SaveChangesAsync(); return sale.Id;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CancellationPreservesInvoice_RetriesOnce_AndAllowsOnlyActualRefund(bool restock)
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        var id = await Seed(database, true); var request = Request(restock);
        await using (var db = database.NewContext())
        {
            var result = await Repo(db).CancelAsync(id, request, "approver");
            Assert.Equal("Cancelled", result.Status); Assert.Equal(20m, result.TotalAmount);
            Assert.Equal(0m, result.NetTotal); Assert.Equal(0m, result.BalanceAmount); Assert.Equal(10m, result.RefundDue);
            Assert.Equal(result.Id, (await Repo(db).CancelAsync(id, request, "approver")).Id);
            await Assert.ThrowsAsync<InvalidOperationException>(() => Repo(db).CancelAsync(id, request with { IdempotencyKey = Guid.NewGuid() }, "approver"));
            var payments = new PaymentRepository(db, Audit(db));
            var summary = (await payments.GetBySaleAsync(id))!;
            Assert.Equal(20m, summary.TotalAmount); Assert.Equal(0m, summary.NetTotal); Assert.Equal(10m, summary.RefundDue);
            await Assert.ThrowsAsync<InvalidOperationException>(() => payments.RecordAsync(id, new(PaymentMethod.Cash, 1m, null, Guid.NewGuid())));
            var returns = new SaleReturnRepository(db, Audit(db));
            var itemId = await db.SaleItems.Select(x => x.Id).SingleAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => returns.CreateAsync(id, new("Not allowed", Guid.NewGuid(), [new(itemId, 1, false)])));
            var refundRequest = new Pharmacy.Application.Returns.RefundRequest(RefundMethod.Cash, 10m, null, Guid.NewGuid());
            var refund = await returns.RefundAsync(id, refundRequest);
            Assert.Equal(refund, await returns.RefundAsync(id, refundRequest));
            await Assert.ThrowsAsync<InvalidOperationException>(() => returns.RefundAsync(id, refundRequest with { IdempotencyKey = Guid.NewGuid(), Amount = 1m }));
        }
        await using var verify = database.NewContext();
        Assert.Equal(restock ? 10 : 8, (await verify.MedicineBatches.SingleAsync()).Quantity);
        Assert.Equal(restock ? 1 : 0, await verify.StockMovements.CountAsync(x => x.MovementType == StockMovementType.SaleCancellation));
        Assert.Equal(1, await verify.AuditLogs.CountAsync(x => x.Action == "SaleCancelled"));
        Assert.Equal(1, await verify.SaleRefunds.CountAsync());
        var report = await new ReportRepository(verify).GetSalesReportAsync(BusinessClock.Today, BusinessClock.Today);
        Assert.Equal(20m, report.TotalRevenue); Assert.Equal(20m, report.ReturnCredits); Assert.Equal(0m, report.NetSales);
        Assert.Equal(10m, report.Collections); Assert.Equal(10m, report.Refunds);
        foreach (var kind in new[] { "sales", "gst", "medicines", "profit" })
            Assert.NotEmpty((await new AdvancedReportRepository(verify).GetAsync(kind, BusinessClock.Today, BusinessClock.Today)).Tables);
    }

    [Fact]
    public async Task RestockingRequiresCustodyAndInspection_AndRejectsExpiredBatches()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(); var id = await Seed(database);
        await using var db = database.NewContext();
        await Assert.ThrowsAsync<ArgumentException>(() => Repo(db).CancelAsync(id, Request() with { MedicinesNotDispensed = false }, "approver"));
        await Assert.ThrowsAsync<ArgumentException>(() => Repo(db).CancelAsync(id, Request() with { InspectionApproved = false }, "approver"));
        await db.MedicineBatches.ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiryDate, BusinessClock.Today));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Repo(db).CancelAsync(id, Request(), "approver"));
        Assert.Equal(SaleStatus.Completed, (await db.Sales.AsNoTracking().SingleAsync()).Status);
        await Repo(db).CancelAsync(id, Request(false), "approver");
        Assert.Equal(8, (await db.MedicineBatches.AsNoTracking().SingleAsync()).Quantity);
    }

    [Fact]
    public async Task ExistingReturnPreventsCancellation()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(); var id = await Seed(database);
        await using var db = database.NewContext(); var item = await db.SaleItems.SingleAsync();
        await new SaleReturnRepository(db, Audit(db)).CreateAsync(id, new("Customer return", Guid.NewGuid(), [new(item.Id, 1, false)]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Repo(db).CancelAsync(id, Request(), "approver"));
        Assert.Equal(SaleStatus.Completed, (await db.Sales.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task AuditFailureRollsBackStatusStockAndMovements()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(); var id = await Seed(database);
        await using (var db = database.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(() => new SaleRepository(db, new FailingAudit()).CancelAsync(id, Request(), "approver"));
        await using var verify = database.NewContext();
        Assert.Equal(SaleStatus.Completed, (await verify.Sales.SingleAsync()).Status);
        Assert.Equal(8, (await verify.MedicineBatches.SingleAsync()).Quantity);
        Assert.False(await verify.StockMovements.AnyAsync(x => x.MovementType == StockMovementType.SaleCancellation));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentCancellationsRestoreStockOnlyOnce_AndCannotCrossBranches(bool sameKey)
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(); var id = await Seed(database);
        var request = Request();
        async Task<bool> Cancel()
        {
            await using var db = database.NewContext();
            try { await Repo(db).CancelAsync(id, sameKey ? request : Request(), "approver"); return true; }
            catch (InvalidOperationException) { return false; }
        }
        Assert.Equal(sameKey ? 2 : 1, (await Task.WhenAll(Cancel(), Cancel())).Count(success => success));
        await using var verify = database.NewContext();
        Assert.Equal(10, (await verify.MedicineBatches.SingleAsync()).Quantity);
        await using var other = database.NewContext(new Pharmacy.Tests.Branches.BranchIsolationTests.Context(2));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Repo(other).CancelAsync(id, Request(), "approver"));
    }
    [Fact]
    public async Task CancellationUsesItsOwnReportDate_WithoutRewritingOriginalInvoiceDate()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(); var id = await Seed(database);
        await using var db = database.NewContext();
        await db.Sales.ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAtUtc, DateTime.UtcNow.AddDays(-2)));
        await Repo(db).CancelAsync(id, Request(), "approver");
        var reports = new ReportRepository(db);
        var original = await reports.GetSalesReportAsync(BusinessClock.Today.AddDays(-2), BusinessClock.Today.AddDays(-2));
        var cancellation = await reports.GetSalesReportAsync(BusinessClock.Today, BusinessClock.Today);
        Assert.Equal(20m, original.NetSales); Assert.Equal(0m, original.ReturnCredits);
        Assert.Equal(0m, cancellation.TotalRevenue); Assert.Equal(20m, cancellation.ReturnCredits); Assert.Equal(-20m, cancellation.NetSales);
        var gst = await new AdvancedReportRepository(db).GetAsync("gst", BusinessClock.Today, BusinessClock.Today);
        Assert.Single(gst.Tables[0].Rows);
        Assert.Equal(-20m, gst.Tables[0].Rows[0][6]);
    }
    [Fact]
    public async Task ConcurrentReceiptAndCancellationLeaveAConsistentRefundLiability()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(); var id = await Seed(database);
        async Task<bool> Pay()
        {
            await using var db = database.NewContext();
            try { await new PaymentRepository(db, Audit(db)).RecordAsync(id, new(PaymentMethod.Cash, 20m, null, Guid.NewGuid())); return true; }
            catch (InvalidOperationException) { return false; }
        }
        async Task Cancel()
        {
            await using var db = database.NewContext(); await Repo(db).CancelAsync(id, Request(), "approver");
        }
        var receipt = Pay(); var cancellation = Cancel();
        await Task.WhenAll(receipt, cancellation);
        await using var verify = database.NewContext();
        var sale = (await Repo(verify).GetByIdAsync(id))!;
        Assert.Equal("Cancelled", sale.Status); Assert.Equal(0m, sale.BalanceAmount);
        Assert.Equal(await receipt ? 20m : 0m, sale.RefundDue);
        Assert.Equal(10, (await verify.MedicineBatches.SingleAsync()).Quantity);
    }
    private sealed class FailingAudit : IAuditService
    {
        public Task WriteAsync(string action, string entityType, string? entityId = null, object? oldValues = null,
            object? newValues = null, bool saveImmediately = true, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Simulated audit failure");
    }
}

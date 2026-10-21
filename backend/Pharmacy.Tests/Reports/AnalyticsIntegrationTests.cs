using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Reports;
using Pharmacy.Domain.Entities;
using Pharmacy.Domain.Entities.Payments;
using Pharmacy.Domain.Entities.Sales;
using Pharmacy.Domain.Entities.Sales.Returns;
using Pharmacy.Infrastructure.Reports;
using Pharmacy.IntegrationTests;

namespace Pharmacy.Tests.Reports;

public sealed class AnalyticsIntegrationTests
{
    [Fact]
    public async Task ReportsUseEventDatesAndCurrentInvoiceBalances_WithoutChangingStock()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await using var db = database.NewContext();
        var medicine = new Medicine("Analytics", "Generic", "Manufacturer", "Tablets");
        db.Medicines.Add(medicine); await db.SaveChangesAsync();
        var batch = new MedicineBatch(medicine.Id, "ANALYTICS", BusinessDates.Today.AddDays(20), 5, 100, 7);
        db.MedicineBatches.Add(batch); await db.SaveChangesAsync();
        var sale = new Sale("ANALYTICS-1");
        sale.AddItem(new SaleItem(medicine.Id, batch.Id, 1, 100, medicine.Name, null, 0));
        db.Sales.Add(sale); await db.SaveChangesAsync();
        var date = new DateOnly(2026, 10, 21);
        var (start, end) = BusinessDates.Range(date, date);
        await db.Sales.ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAtUtc, start.AddTicks(-1)));
        var payment = new Payment(sale.Id, PaymentMethod.Cash, 50, null, Guid.NewGuid());
        db.Payments.Add(payment);
        var returned = new SaleReturn(sale.Id, "Damaged", Guid.NewGuid());
        returned.Items.Add(new SaleReturnItem(sale.Items.Single().Id, 1, false, 100, ReturnDisposition.Damaged));
        db.SaleReturns.Add(returned);
        db.SaleRefunds.Add(new SaleRefund(sale.Id, RefundMethod.Cash, 20, null, Guid.NewGuid()));
        await db.SaveChangesAsync();
        await db.Payments.ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAtUtc, start));
        await db.SaleReturns.ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAtUtc, end.AddTicks(-1)));
        await db.SaleRefunds.ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAtUtc, start));
        var repository = new ReportRepository(db);
        var report = await repository.GetSalesReportAsync(date, date);
        Assert.Equal(0, report.TotalSales);
        Assert.Equal(0m, report.TotalRevenue);
        Assert.Equal(100m, report.ReturnCredits);
        Assert.Equal(-100m, report.NetSales);
        Assert.Equal(50m, report.Collections);
        Assert.Equal(20m, report.Refunds);
        Assert.Equal(30m, report.NetCollections);
        Assert.Equal(0m, report.RefundDue); // Original invoice is outside the cohort.
        Assert.Equal(100m, Assert.Single(report.DailySales).ReturnCredits);
        var cohort = await repository.GetSalesReportAsync(date.AddDays(-1), date);
        Assert.Equal(30m, cohort.RefundDue);
        Assert.Equal(0m, cohort.OutstandingBalance);
        Assert.Equal(30m, cohort.CollectionsByMethod.Single(x => x.Method == "Cash").NetCollections);
        Assert.Equal(7, (await db.MedicineBatches.AsNoTracking().SingleAsync()).Quantity);
        Assert.Empty(await db.StockMovements.ToListAsync());
        var empty = await repository.GetSalesReportAsync(date.AddDays(2), date.AddDays(3));
        Assert.Equal(2, empty.DailySales.Count);
        Assert.All(empty.DailySales, x => Assert.Equal(0m, x.NetSales));
    }
}

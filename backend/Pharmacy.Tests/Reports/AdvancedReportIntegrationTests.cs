using Pharmacy.Application.Reports;
using Pharmacy.Domain.Entities;
using Pharmacy.Domain.Entities.Sales;
using Pharmacy.Domain.Entities.Sales.Returns;
using Pharmacy.Infrastructure.Reports;
using Pharmacy.IntegrationTests;

namespace Pharmacy.Tests.Reports;

public sealed class AdvancedReportIntegrationTests
{
    [Theory]
    [InlineData(false, -20)]
    [InlineData(true, 0)]
    public async Task ReturnsReverseGst_AndRecoverCostOnlyWhenRestocked(bool restock, int expectedProfit)
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await using var db = database.NewContext();
        var medicine = new Medicine("Report medicine", "Generic", "Manufacturer", "Tablets");
        db.Medicines.Add(medicine); await db.SaveChangesAsync();
        var batch = new MedicineBatch(medicine.Id, "REPORT", BusinessDates.Today.AddYears(1), 20, 105, 5);
        db.MedicineBatches.Add(batch); await db.SaveChangesAsync();
        var sale = new Sale("ADVANCED-TEST");
        sale.AddItem(new SaleItem(medicine.Id, batch.Id, 1, 105, medicine.Name, "3004", 5));
        db.Sales.Add(sale); await db.SaveChangesAsync();
        var returned = new SaleReturn(sale.Id, "Return", Guid.NewGuid());
        returned.Items.Add(new SaleReturnItem(sale.Items.Single().Id, 1, restock, 105,
            restock ? ReturnDisposition.Saleable : ReturnDisposition.Damaged, restock, restock ? "inspector" : null));
        db.SaleReturns.Add(returned); await db.SaveChangesAsync();
        var repository = new AdvancedReportRepository(db);
        var profit = await repository.GetAsync("profit", BusinessDates.Today, BusinessDates.Today);
        Assert.Equal((decimal)expectedProfit, profit.Tables[0].Rows.Single(x => Equals(x[0], "Gross profit"))[1]);
        var gst = await repository.GetAsync("gst", BusinessDates.Today, BusinessDates.Today);
        var row = Assert.Single(gst.Tables[0].Rows);
        Assert.Equal(0m, row[6]); Assert.Equal(0m, row[7]);
        var medicines = await repository.GetAsync("medicines", BusinessDates.Today, BusinessDates.Today);
        Assert.Equal(0m, medicines.Tables[0].Rows.Single()[7]);
        var stock = await repository.GetAsync("stock", BusinessDates.Today, BusinessDates.Today);
        Assert.Equal(100m, stock.Tables[0].Rows.Single(x => Equals(x[0], "All stock at cost"))[1]);
    }
}

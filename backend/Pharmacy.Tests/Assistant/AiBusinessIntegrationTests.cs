using Pharmacy.Application.Assistant;
using Pharmacy.Domain.Entities;
using Pharmacy.Domain.Entities.Purchasing;
using Pharmacy.Domain.Entities.Sales;
using Pharmacy.Domain.Entities.Sales.Returns;
using Pharmacy.Infrastructure.Assistant;
using Pharmacy.IntegrationTests;
using Pharmacy.Tests.Branches;

namespace Pharmacy.Tests.Assistant;

public sealed class AiBusinessIntegrationTests
{
    [Fact]
    public async Task BranchInventoryAndDemandNeverIncludeOtherBranchStockSalesOrPurchases()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        int medicineId, secondId;
        await using (var main = database.NewContext())
        {
            var medicine = new Medicine("AI shared medicine", "Generic", "Manufacturer", "Tablets");
            var second = new Branch { Code = "AI-SECOND", Name = "Second branch" };
            var supplier = new Supplier("AI supplier", null, null, null);
            main.Medicines.Add(medicine); main.Branches.Add(second); main.Suppliers.Add(supplier); await main.SaveChangesAsync();
            medicineId = medicine.Id; secondId = second.Id;
            var batch = new MedicineBatch(medicineId, "MAIN", Pharmacy.Domain.BusinessClock.Today.AddYears(1), 2, 5, 25);
            main.MedicineBatches.Add(batch); await main.SaveChangesAsync();
            var sale = new Sale("AI-SALE"); sale.AddItem(new SaleItem(medicineId, batch.Id, 10, 5, medicine.Name, null, 0));
            main.Sales.Add(sale); await main.SaveChangesAsync();
            var returned = new SaleReturn(sale.Id, "Damaged return", Guid.NewGuid());
            returned.Items.Add(new SaleReturnItem(sale.Items.Single().Id, 2, false, 10, ReturnDisposition.Damaged, false, null));
            main.SaleReturns.Add(returned);
            var purchase = new PurchaseInvoice(supplier.Id, "AI-PENDING", Pharmacy.Domain.BusinessClock.Today);
            purchase.AddItem(new PurchaseItem(medicineId, "FUTURE", Pharmacy.Domain.BusinessClock.Today.AddYears(1), 7, 2, 5));
            main.PurchaseInvoices.Add(purchase); await main.SaveChangesAsync();
            var facts = Assert.Single(await new AiBusinessData(main).DemandAsync(30, 30, default));
            Assert.Equal(25, facts.Available); Assert.Equal(8, facts.Sold); Assert.Equal(7, facts.PendingPurchase);
        }
        await using var other = database.NewContext(new BranchIsolationTests.Context(secondId));
        other.MedicineBatches.Add(new MedicineBatch(medicineId, "OTHER", Pharmacy.Domain.BusinessClock.Today.AddYears(1), 2, 5, 3));
        await other.SaveChangesAsync();
        var repository = new AiBusinessData(other);
        var inventory = Assert.Single(await repository.InventoryAsync("stock", "", 30, default));
        Assert.Equal("OTHER", inventory.Batch); Assert.Equal(3, inventory.Quantity);
        var demand = Assert.Single(await repository.DemandAsync(30, 30, default));
        Assert.Equal(3, demand.Available); Assert.Equal(0, demand.Sold); Assert.Equal(0, demand.PendingPurchase);
    }
}

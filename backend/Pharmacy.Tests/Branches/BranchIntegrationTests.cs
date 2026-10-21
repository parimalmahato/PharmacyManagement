using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Domain.Entities;
using Pharmacy.Domain.Entities.Sales;
using Pharmacy.Domain.Entities.Payments;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Payments;
using Pharmacy.Infrastructure.Auditing;
using Pharmacy.IntegrationTests;

namespace Pharmacy.Tests.Branches;

public sealed class BranchIntegrationTests
{
    [Fact]
    public async Task SameBatchNumbersAreIndependent_AndPaymentsCannotCrossBranches()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        int branchId, medicineId, saleId;
        await using (var main = database.NewContext())
        {
            var branch = new Branch { Code = "SECOND", Name = "Second branch" }; main.Branches.Add(branch);
            var medicine = new Medicine("Shared medicine", "Generic", "Manufacturer", "Tablets"); main.Medicines.Add(medicine);
            await main.SaveChangesAsync(); branchId = branch.Id; medicineId = medicine.Id;
            var batch = new MedicineBatch(medicineId, "SHARED-LOT", Pharmacy.Domain.BusinessClock.Today.AddYears(1), 5, 10, 4);
            main.MedicineBatches.Add(batch); await main.SaveChangesAsync();
            var sale = new Sale("MAIN-INVOICE"); sale.AddItem(new SaleItem(medicineId, batch.Id, 1, 10, medicine.Name, null, 0));
            main.Sales.Add(sale); await main.SaveChangesAsync(); saleId = sale.Id;
        }
        await using var second = database.NewContext(new BranchIsolationTests.Context(branchId));
        Assert.Single(await second.Medicines.ToListAsync());
        Assert.Empty(await second.MedicineBatches.ToListAsync());
        second.MedicineBatches.Add(new MedicineBatch(medicineId, "SHARED-LOT", Pharmacy.Domain.BusinessClock.Today.AddYears(1), 5, 10, 9));
        await second.SaveChangesAsync();
        Assert.Equal(9, (await second.MedicineBatches.SingleAsync()).Quantity);
        var payments = new PaymentRepository(second, new AuditService(second, new HttpContextAccessor()));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => payments.RecordAsync(saleId, new(PaymentMethod.Cash, 10, null, Guid.NewGuid())));
        await using var first = database.NewContext();
        Assert.Equal(4, (await first.MedicineBatches.SingleAsync()).Quantity);
    }
}

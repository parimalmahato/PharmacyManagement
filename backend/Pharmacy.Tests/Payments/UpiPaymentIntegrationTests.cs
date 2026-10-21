using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Payments;
using Pharmacy.Application.Sales;
using Pharmacy.Application.Settings;
using Pharmacy.Domain;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Auditing;
using Pharmacy.Infrastructure.Sales;
using Pharmacy.Infrastructure.Settings;
using Pharmacy.IntegrationTests;

namespace Pharmacy.Tests.Payments;

public sealed class UpiPaymentIntegrationTests
{
    [Fact]
    public async Task UsesOnlyActiveBranchSettings_WithoutWritingPaymentOrStock()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await using var db = database.NewContext();
        var audit = new AuditService(db, new HttpContextAccessor());
        var settings = new PharmacySettingsRepository(db, audit);
        var request = new UpdatePharmacySettingsRequest("Pharmacy", "Street", "9876543210", null, null, "UPI", false, 0,
            UpiEnabled: true, UpiId: "main@bank", UpiPayeeName: "Main Pharmacy");
        var saved = await settings.UpdateAsync(request);
        var medicine = new Medicine("UPI test medicine", "Generic", "Maker", "Tablet"); db.Medicines.Add(medicine);
        var branch = new Branch { Code = "UPI2", Name = "UPI second branch" }; db.Branches.Add(branch); await db.SaveChangesAsync();
        db.MedicineBatches.Add(new MedicineBatch(medicine.Id, "UPI-LOT", BusinessClock.Today.AddYears(1), 5, 10, 5)); await db.SaveChangesAsync();
        var sales = new SaleRepository(db, audit);
        var sale = await sales.CreateAsync(new CreateSaleRequest([new(medicine.Id, 1)]));
        var stock = (await db.MedicineBatches.AsNoTracking().SingleAsync()).Quantity;
        var auditCount = await db.AuditLogs.CountAsync();
        var service = new UpiPaymentRequestService(sales, settings);
        var qr = await service.GetAsync(sale.Id, 5m);
        Assert.Equal("main@bank", qr.PayeeUpiId); Assert.Equal(5m, qr.Amount); Assert.Equal(10m, qr.OutstandingBalance);
        Assert.False(await db.Payments.AnyAsync()); Assert.Equal(auditCount, await db.AuditLogs.CountAsync());
        Assert.Equal(stock, (await db.MedicineBatches.AsNoTracking().SingleAsync()).Quantity);
        await Assert.ThrowsAsync<ArgumentException>(() => settings.UpdateAsync(request with { Revision = saved.Revision, UpiId = "bad&pa=other@bank" }));
        await using var other = database.NewContext(new Pharmacy.Tests.Branches.BranchIsolationTests.Context(branch.Id));
        var otherAudit = new AuditService(other, new HttpContextAccessor());
        var otherSettings = new PharmacySettingsRepository(other, otherAudit);
        Assert.Null(await otherSettings.GetAsync());
        await otherSettings.UpdateAsync(request with { UpiId = "second@bank", UpiPayeeName = "Second Pharmacy" });
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new UpiPaymentRequestService(new SaleRepository(other, otherAudit), otherSettings).GetAsync(sale.Id, null));
        Assert.Equal("main@bank", (await settings.GetAsync())!.UpiId);
    }
}

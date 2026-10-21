using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pharmacy.Application.Settings;
using Pharmacy.Application.Sales;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Auditing;
using Pharmacy.Infrastructure.Notifications;
using Pharmacy.Infrastructure.Reports;
using Pharmacy.Infrastructure.Sales;
using Pharmacy.Infrastructure.Settings;
using Pharmacy.IntegrationTests;

namespace Pharmacy.Tests.Settings;

public sealed class SettingsIntegrationTests
{
    private static UpdatePharmacySettingsRequest Request() => new("Pharmacy", "Street", "9876543210", null, null, "RX", false, 0,
        LowStockThreshold: 20, ExpiryAlertsEnabled: false, ReceiptFooter: "Custom footer");

    [Fact]
    public async Task SettingsPersistAndRejectStaleUpdates_WithoutResettingInvoiceSequence()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await using var db = database.NewContext();
        var audit = new AuditService(db, new HttpContextAccessor());
        var settings = new PharmacySettingsRepository(db, audit);
        var saved = await settings.UpdateAsync(Request());
        var changed = await settings.UpdateAsync(Request() with { Revision = saved.Revision, PharmacyName = "Changed" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => settings.UpdateAsync(Request() with { Revision = saved.Revision }));
        Assert.NotEqual(saved.Revision, changed.Revision);
        Assert.Equal("Custom footer", (await settings.GetAsync())!.ReceiptFooter);
        Assert.Equal(1, await db.PharmacySettings.CountAsync());
        Assert.Equal(2, await db.AuditLogs.CountAsync(x => x.Action == "PharmacySettingsUpdated"));
    }

    [Fact]
    public async Task CheckoutConsumesNumberAndSettingsDriveStockAndAlertQueries()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await using var db = database.NewContext();
        var audit = new AuditService(db, new HttpContextAccessor());
        await new PharmacySettingsRepository(db, audit).UpdateAsync(Request());
        var medicine = new Medicine("Test", "Generic", "Manufacturer", "Tablets");
        medicine.ConfigureTax("3004", 5);
        db.Medicines.Add(medicine); await db.SaveChangesAsync();
        db.MedicineBatches.Add(new MedicineBatch(medicine.Id, "SETTINGS", DateOnly.FromDateTime(DateTime.UtcNow).AddDays(20), 5, 10, 20));
        await db.SaveChangesAsync();
        var saleRepository = new SaleRepository(db, audit);
        var first = await saleRepository.CreateAsync(new CreateSaleRequest([new(medicine.Id, 1)]));
        var second = await saleRepository.CreateAsync(new CreateSaleRequest([new(medicine.Id, 1)]));
        Assert.Equal("RX-000001", first.InvoiceNumber); Assert.Equal("RX-000002", second.InvoiceNumber);
        Assert.Equal(0m, first.GstAmount);
        Assert.Equal(3, (await db.PharmacySettings.SingleAsync()).NextInvoiceNumber);
        Assert.Single(await new ReportRepository(db).GetLowStockBatchesAsync());
        await new ExpiryNotificationProcessor(db, NullLogger<ExpiryNotificationProcessor>.Instance).ProcessAsync();
        Assert.Empty(await db.ExpiryNotifications.ToListAsync());
    }
}

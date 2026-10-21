using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Auditing;
using Pharmacy.Application.Reports;
using Pharmacy.Domain;
using Pharmacy.Domain.Entities;
using Pharmacy.Domain.Entities.Purchasing;
using Pharmacy.Domain.Entities.Sales;
using Pharmacy.Infrastructure.Auditing;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Reports;
using Pharmacy.IntegrationTests;

namespace Pharmacy.Tests.Reports;

public sealed class GstPreparationIntegrationTests
{
    private const string Registration = "27ABCDE1234F1Z5";
    private static int Year => BusinessClock.Today.Year;
    private static int Month => BusinessClock.Today.Month;
    private static GstPreparationRepository Repo(PharmacyDbContext db, IAuditService? audit = null) => new(db, audit ?? new AuditService(db, new HttpContextAccessor()));
    private static async Task Seed(SqlServerTestDatabase database, bool purchase = false)
    {
        await using var db = database.NewContext();
        db.PharmacySettings.Add(new PharmacySettings { PharmacyName = "Pharmacy", GstEnabled = true, Gstin = Registration });
        var medicine = new Medicine("Tax medicine", "Generic", "Maker", "Tablet"); medicine.ConfigureTax("3004", 5m);
        db.Medicines.Add(medicine); await db.SaveChangesAsync();
        var batch = new MedicineBatch(medicine.Id, "GST-BATCH", BusinessClock.Today.AddYears(1), 100, 105, 10);
        db.MedicineBatches.Add(batch); await db.SaveChangesAsync();
        var sale = new Sale("GST-00001"); sale.AddItem(new SaleItem(medicine.Id, batch.Id, 1, 105, medicine.Name, "3004", 5));
        db.Sales.Add(sale);
        if (purchase)
        {
            var supplier = new Supplier("Tax supplier", null, null, null); db.Suppliers.Add(supplier); await db.SaveChangesAsync();
            var invoice = new PurchaseInvoice(supplier.Id, "SUP-001", BusinessClock.Today);
            invoice.AddItem(new PurchaseItem(medicine.Id, "GST-RECEIPT", BusinessClock.Today.AddYears(1), 1, 100, 105)); invoice.MarkReceived(); db.PurchaseInvoices.Add(invoice);
        }
        await db.SaveChangesAsync();
    }
    private static async Task<GstPreparationDto> Get(SqlServerTestDatabase database)
    { await using var db = database.NewContext(); return await Repo(db).GetAsync(Year, Month); }
    private static GstTaxDetails Tax(GstSourceDocument doc, bool eligible = false) => new(Registration, doc.Kind == "PurchaseInvoice" ? "27FGHIJ5678K1Z5" : null,
        "27", "Taxable", doc.Kind is "SalesReturn" or "SaleCancellation" ? $"CN-{doc.Id}" : doc.Number, doc.Date,
        doc.Kind == "PurchaseInvoice" ? 105 : doc.BookAmount, doc.RecordedTaxable ?? 100, 2.5m, 2.5m, 0, 0, eligible);
    private static async Task Save(SqlServerTestDatabase database, GstSourceDocument doc, bool eligible = false)
    { await using var db = database.NewContext(); await Repo(db).SaveDetailsAsync(Year, Month, doc.Kind, doc.Id, new(Tax(doc, eligible), doc.Revision), "accountant"); }

    [Fact]
    public async Task MissingFactsBlockReview_ThenReviewLocks_AndNewSourcesRequireReopen()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(); await Seed(database);
        var p = await Get(database); Assert.Contains(p.Issues, i => i.Code == "MISSING_TAX_DETAILS");
        await using (var db = database.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(() => Repo(db).ReviewAsync(Year, Month, new(p.Fingerprint, p.Revision), "accountant"));
        await Save(database, Assert.Single(p.Documents)); p = await Get(database);
        Assert.DoesNotContain(p.Issues, i => i.Severity == "Error"); Assert.Equal(100m, p.NetOutwardValue); Assert.Equal(2.5m, p.NetCgst);
        await using (var db = database.NewContext()) p = await Repo(db).ReviewAsync(Year, Month, new(p.Fingerprint, p.Revision), "accountant");
        Assert.Equal("Reviewed", p.Status); Assert.False(p.ChangedSinceReview); Assert.NotNull(p.ReviewId);
        await using (var db = database.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(() => Repo(db).SaveDetailsAsync(Year, Month, p.Documents[0].Kind, p.Documents[0].Id, new(Tax(p.Documents[0]), p.Documents[0].Revision), "accountant"));
        await using (var db = database.NewContext())
        {
            var batch = await db.MedicineBatches.SingleAsync();
            var extra = new Sale("GST-LATE"); extra.AddItem(new SaleItem(batch.MedicineId, batch.Id, 1, 105, "Tax medicine", "3004", 5)); db.Sales.Add(extra); await db.SaveChangesAsync();
        }
        p = await Get(database); Assert.True(p.ChangedSinceReview);
        await using (var db = database.NewContext())
        {
            var service = new GstPreparationService(Repo(db), new ReportExporter(), new AdvancedReportRepository(db));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExportAsync(Year, Month, "xlsx"));
            await Repo(db).ReopenAsync(Year, Month, new("Late invoice needs review", p.Revision!.Value), "accountant");
        }
        p = await Get(database); Assert.Equal("Draft", p.Status);
        await using var verify = database.NewContext();
        Assert.Equal(1, await verify.GstReturnReviews.CountAsync()); Assert.Equal(2, await verify.Sales.CountAsync());
        Assert.Equal(10, (await verify.MedicineBatches.SingleAsync()).Quantity);
        Assert.False(await verify.Payments.AnyAsync());
    }

    [Fact]
    public async Task ItcRequiresExactAvailableStatementMatch_AndDraftExportPreservesFacts()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(); await Seed(database, true);
        var p = await Get(database);
        foreach (var doc in p.Documents) await Save(database, doc, doc.Kind == "PurchaseInvoice");
        p = await Get(database); Assert.Contains(p.Issues, x => x.Code == "ITC_UNRECONCILED"); Assert.Equal(0m, p.MatchedEligibleItc);
        var csv = "SupplierGstin,InvoiceNumber,InvoiceDate,TaxableValue,Cgst,Sgst,Igst,Cess,ItcAvailable\n" + $"27FGHIJ5678K1Z5,SUP-001,{BusinessClock.Today:yyyy-MM-dd},100,2.50,2.50,0,0,true";
        await using (var db = database.NewContext()) await Repo(db).ImportAsync(Year, Month, csv, p.Revision, "accountant");
        p = await Get(database); Assert.Equal(5m, p.MatchedEligibleItc); Assert.DoesNotContain(p.Issues, i => i.Severity == "Error");
        await using (var db = database.NewContext())
        {
            var service = new GstPreparationService(Repo(db), new ReportExporter(), new AdvancedReportRepository(db));
            Assert.NotEmpty((await service.ExportAsync(Year, Month, "xlsx")).Content);
            Assert.NotEmpty((await service.ExportAsync(Year, Month, "pdf")).Content);
        }
        await using (var db = database.NewContext()) await Repo(db).ImportAsync(Year, Month, csv.Replace(",true", ",false"), p.Revision, "accountant");
        p = await Get(database); Assert.Equal(0m, p.MatchedEligibleItc); Assert.Contains(p.Issues, x => x.Code == "ITC_UNRECONCILED");
        await using var verify = database.NewContext(); Assert.False(await verify.Payments.AnyAsync()); Assert.Equal(100m, (await verify.PurchaseInvoices.SingleAsync()).TotalAmount);
    }

    [Fact]
    public async Task AuditFailureRollsBackTaxAnnotations_AndBranchCannotReadOtherSources()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(); await Seed(database); var p = await Get(database); var doc = Assert.Single(p.Documents);
        await using (var db = database.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(() => Repo(db, new FailingAudit()).SaveDetailsAsync(Year, Month, doc.Kind, doc.Id, new(Tax(doc), null), "accountant"));
        await using (var db = database.NewContext()) { Assert.False(await db.GstDocumentDetails.AnyAsync()); Assert.False(await db.GstReturnWorkspaces.AnyAsync()); }
        await using var other = database.NewContext(new Pharmacy.Tests.Branches.BranchIsolationTests.Context(2));
        Assert.Empty((await Repo(other).GetAsync(Year, Month)).Documents);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Repo(other).SaveDetailsAsync(Year, Month, doc.Kind, doc.Id, new(Tax(doc), null), "accountant"));
    }

    [Fact]
    public async Task StaleReviewOrAnnotationDoesNotOverwriteANewerVersion()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(); await Seed(database); var p = await Get(database); var old = Assert.Single(p.Documents);
        await Save(database, old);
        await using (var db = database.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(() => Repo(db).SaveDetailsAsync(Year, Month, old.Kind, old.Id, new(Tax(old), old.Revision), "accountant"));
        await using (var db = database.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(() => Repo(db).ReviewAsync(Year, Month, new(p.Fingerprint, p.Revision), "accountant"));
    }
    private sealed class FailingAudit : IAuditService
    {
        public Task WriteAsync(string action, string entityType, string? entityId = null, object? oldValues = null, object? newValues = null,
            bool saveImmediately = true, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Simulated audit failure");
    }

    [Fact]
    public async Task ReviewAuditFailureRollsBackAnAlreadyInsertedSnapshot()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(); await Seed(database);
        var p = await Get(database); await Save(database, Assert.Single(p.Documents)); p = await Get(database);
        await using (var db = database.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(() => Repo(db, new FailingAudit()).ReviewAsync(Year, Month, new(p.Fingerprint, p.Revision), "accountant"));
        var after = await Get(database); Assert.Equal("Draft", after.Status); Assert.Equal(p.Revision, after.Revision);
        await using var verify = database.NewContext(); Assert.False(await verify.GstReturnReviews.AnyAsync());
    }

    [Fact]
    public async Task ConcurrentReviewsCreateOnlyOneSnapshot()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(); await Seed(database);
        var p = await Get(database); await Save(database, Assert.Single(p.Documents)); p = await Get(database);
        async Task<bool> Review()
        {
            await using var db = database.NewContext();
            try { await Repo(db).ReviewAsync(Year, Month, new(p.Fingerprint, p.Revision), "accountant"); return true; }
            catch (InvalidOperationException) { return false; }
        }
        Assert.Single(await Task.WhenAll(Review(), Review()), success => success);
        await using var verify = database.NewContext(); Assert.Equal(1, await verify.GstReturnReviews.CountAsync());
    }

    [Fact]
    public async Task DuplicateBookKeysCannotReuseOneStatementInvoiceForDoubleItc()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(); await Seed(database, true);
        await using (var db = database.NewContext())
        {
            var supplier = new Supplier("Duplicate supplier master", null, null, null); db.Suppliers.Add(supplier); await db.SaveChangesAsync();
            var batch = await db.MedicineBatches.SingleAsync();
            var duplicate = new PurchaseInvoice(supplier.Id, "SUP-001", BusinessClock.Today);
            duplicate.AddItem(new PurchaseItem(batch.MedicineId, "DUPLICATE", BusinessClock.Today.AddYears(1), 1, 100, 105)); duplicate.MarkReceived(); db.PurchaseInvoices.Add(duplicate); await db.SaveChangesAsync();
        }
        var p = await Get(database); foreach (var doc in p.Documents) await Save(database, doc, doc.Kind == "PurchaseInvoice"); p = await Get(database);
        var csv = "SupplierGstin,InvoiceNumber,InvoiceDate,TaxableValue,Cgst,Sgst,Igst,Cess,ItcAvailable\n" + $"27FGHIJ5678K1Z5,SUP-001,{BusinessClock.Today:yyyy-MM-dd},100,2.50,2.50,0,0,true";
        await using (var db = database.NewContext()) await Repo(db).ImportAsync(Year, Month, csv, p.Revision, "accountant");
        p = await Get(database); Assert.Equal(0m, p.MatchedEligibleItc); Assert.Contains(p.Issues, i => i.Code == "DUPLICATE_PURCHASE_KEY");
        Assert.All(p.Reconciliation, row => Assert.Equal(0m, row.EligibleMatchedItc));
    }

    [Fact]
    public async Task PartialReturnCreditsUseCumulativeGstRounding_AndLinkTheOriginalInvoice()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(); await Seed(database);
        await using (var db = database.NewContext())
        {
            var batch = await db.MedicineBatches.SingleAsync();
            var sale = new Sale("GST-ROUND"); sale.AddItem(new SaleItem(batch.MedicineId, batch.Id, 3, .35m, "Tax medicine", "3004", 5)); db.Sales.Add(sale); await db.SaveChangesAsync();
            var item = sale.Items.Single();
            var returns = new Pharmacy.Infrastructure.Sales.SaleReturnRepository(db, new AuditService(db, new HttpContextAccessor()));
            for (var index = 0; index < 3; index++) await returns.CreateAsync(sale.Id, new("Partial return", Guid.NewGuid(), [new(item.Id, 1, false)]));
        }
        var p = await Get(database); var credits = p.Documents.Where(x => x.Kind == "SalesReturn").ToList();
        Assert.Equal(3, credits.Count); Assert.Equal(.05m, credits.Sum(x => x.RecordedGst)); Assert.Equal(1m, credits.Sum(x => x.RecordedTaxable));
        Assert.All(credits, x => Assert.Equal("GST-ROUND", x.RelatedInvoice));
    }
}

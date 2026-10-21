using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using Pharmacy.Application.Auditing;
using Pharmacy.Application.Reports;
using Pharmacy.Domain.Entities.Gst;
using Pharmacy.Domain.Entities.Purchasing;
using Pharmacy.Domain.Entities.Sales;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Reports;

/// <summary>
/// Persists audited GST annotations, normalized statement imports and review fingerprints within branch-period transaction boundaries.
/// </summary>
public sealed class GstPreparationRepository(PharmacyDbContext db, IAuditService audit) : IGstPreparationRepository
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static DateOnly Date(DateTime value) => DateOnly.FromDateTime(value.AddMinutes(330));
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json))));
    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private async Task<List<GstSourceDocument>> Sources(int year, int month, CancellationToken ct)
    {
        var (first, last) = GstPreparationRules.Period(year, month); var (start, end) = BusinessDates.Range(first, last);
        var sales = await db.Sales.AsNoTracking().Include(x => x.Items).Include(x => x.Customer)
            .Where(x => (x.CreatedAtUtc >= start && x.CreatedAtUtc < end) || (x.CancelledAtUtc >= start && x.CancelledAtUtc < end)).Take(2001).ToListAsync(ct);
        var purchases = await db.PurchaseInvoices.AsNoTracking().Include(x => x.Supplier).Where(x => x.Status == PurchaseStatus.Received && x.InvoiceDate >= first && x.InvoiceDate <= last).Take(2001).ToListAsync(ct);
        var headers = await db.SaleReturns.AsNoTracking().Include(x => x.Items).Where(x => x.CreatedAtUtc >= start && x.CreatedAtUtc < end).Take(2001).ToListAsync(ct);
        if (sales.Count + purchases.Count + headers.Count > 2000) throw new InvalidOperationException("Monthly preparation supports at most 2,000 source documents per branch. Split large-volume processing in an accountant workflow.");
        var output = new List<GstSourceDocument>();
        foreach (var sale in sales)
        {
            bool missing = sale.Items.Any(x => string.IsNullOrWhiteSpace(x.HsnCode));
            bool mixed = sale.Items.Any(x => x.GstPercentage == 0 && x.LineTotal > 0) && sale.Items.Any(x => x.GstPercentage > 0 && x.LineTotal > 0);
            if (sale.CreatedAtUtc >= start && sale.CreatedAtUtc < end)
                output.Add(new("SalesInvoice", sale.Id, Date(sale.CreatedAtUtc), sale.InvoiceNumber, sale.Customer?.Name ?? "Walk-in", sale.TotalAmount, sale.TaxableAmount, sale.GstAmount, missing, mixed));
            if (sale.CancelledAtUtc >= start && sale.CancelledAtUtc < end)
                output.Add(new("SaleCancellation", sale.Id, Date(sale.CancelledAtUtc!.Value), $"Cancellation of {sale.InvoiceNumber}", sale.Customer?.Name ?? "Walk-in", sale.TotalAmount, sale.TaxableAmount, sale.GstAmount, missing, mixed, RelatedInvoice: sale.InvoiceNumber));
        }
        foreach (var p in purchases) output.Add(new("PurchaseInvoice", p.Id, p.InvoiceDate, p.InvoiceNumber, p.Supplier.Name, p.TotalAmount, null, null, false, false));
        var ids = headers.SelectMany(x => x.Items).Select(x => x.SaleItemId).Distinct().ToList();
        var returnSaleIds = headers.Select(x => x.SaleId).Distinct().ToList();
        var relatedInvoices = await db.Sales.AsNoTracking().Where(x => returnSaleIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.InvoiceNumber, ct);
        var originals = await db.SaleItems.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var historical = await (from item in db.SaleReturnItems.AsNoTracking() join header in db.SaleReturns on item.SaleReturnId equals header.Id
                                where ids.Contains(item.SaleItemId) && header.CreatedAtUtc < end
                                orderby header.CreatedAtUtc, header.Id, item.Id select new { Item = item, header.CreatedAtUtc }).ToListAsync(ct);
        var prior = new Dictionary<int, decimal>(); var gstByReturn = new Dictionary<long, decimal>();
        foreach (var row in historical)
        {
            var original = originals[row.Item.SaleItemId]; var before = prior.GetValueOrDefault(original.Id); var after = before + row.Item.Amount;
            var gst = original.LineTotal == 0 ? 0m : Round(after * original.GstAmount / original.LineTotal) - Round(before * original.GstAmount / original.LineTotal);
            prior[original.Id] = after;
            if (row.CreatedAtUtc >= start) gstByReturn[row.Item.SaleReturnId] = gstByReturn.GetValueOrDefault(row.Item.SaleReturnId) + gst;
        }
        foreach (var header in headers)
        {
            var amount = header.Items.Sum(x => x.Amount); var gst = gstByReturn.GetValueOrDefault(header.Id);
            output.Add(new("SalesReturn", header.Id, Date(header.CreatedAtUtc), $"Return #{header.Id}", $"Sale #{header.SaleId}", amount, amount - gst, gst,
                header.Items.Any(x => string.IsNullOrWhiteSpace(originals[x.SaleItemId].HsnCode)),
                header.Items.Any(x => originals[x.SaleItemId].GstPercentage == 0 && x.Amount > 0) && header.Items.Any(x => originals[x.SaleItemId].GstPercentage > 0 && x.Amount > 0), RelatedInvoice: relatedInvoices[header.SaleId]));
        }
        if (output.Count > 2000) throw new InvalidOperationException("Monthly preparation supports at most 2,000 source documents per branch.");
        return output.OrderBy(x => x.Date).ThenBy(x => x.Kind).ThenBy(x => x.Id).ToList();
    }

    /// <summary>
    /// Loads the branch-period source register, validation and reconciliation and detects changes against the last reviewed fingerprint.
    /// </summary>
    public async Task<GstPreparationDto> GetAsync(int year, int month, CancellationToken ct = default)
    {
        _ = GstPreparationRules.Period(year, month);
        var workspace = await db.GstReturnWorkspaces.AsNoTracking().SingleOrDefaultAsync(x => x.Year == year && x.Month == month, ct);
        var settings = await db.PharmacySettings.AsNoTracking().SingleOrDefaultAsync(ct);
        var sources = await Sources(year, month, ct);
        var kinds = sources.Select(x => x.Kind).Distinct().ToList(); var ids = sources.Select(x => x.Id).Distinct().ToList();
        var details = await db.GstDocumentDetails.AsNoTracking().Where(x => kinds.Contains(x.SourceKind) && ids.Contains(x.SourceId)).ToListAsync(ct);
        var documents = sources.Select(x => {
            var saved = details.SingleOrDefault(d => d.SourceKind == x.Kind && d.SourceId == x.Id);
            return x with { TaxDetails = saved is null ? null : JsonSerializer.Deserialize<GstTaxDetails>(saved.DetailsJson, Json), Revision = saved?.Revision };
        }).ToList();
        var issues = new List<GstIssue> { new("Warning", "BRANCH_WORKSHEET", "This is a branch worksheet, not a complete GST registration return. Consolidate every branch of this GSTIN and validate statutory tables before filing.") };
        if (settings is null || !settings.GstEnabled || !GstPreparationRules.GstinValid(settings.Gstin)) issues.Add(new("Error", "REGISTRATION", "Enable GST and configure a valid branch GSTIN before review."));
        var valid = new List<GstSourceDocument>();
        foreach (var doc in documents)
        {
            if (doc.TaxDetails is null) { issues.Add(new("Error", "MISSING_TAX_DETAILS", "Enter tax classification and original-document facts; no tax-head allocation or purchase ITC is inferred.", doc.Kind, doc.Id)); continue; }
            try { GstPreparationRules.Validate(doc, doc.TaxDetails, settings?.Gstin); valid.Add(doc); }
            catch (ArgumentException ex) { issues.Add(new("Error", "INVALID_TAX_DETAILS", ex.Message, doc.Kind, doc.Id)); }
            if (doc.MissingHsn) issues.Add(new("Error", "MISSING_HSN", "Original invoice HSN snapshots are missing. Resolve with the accountant; annotations cannot rewrite the invoice.", doc.Kind, doc.Id));
        }
        var duplicateNumbers = valid.Where(x => GstPreparationRules.IsCredit(x.Kind)).GroupBy(x => x.TaxDetails!.DocumentNumber, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1);
        foreach (var duplicate in duplicateNumbers)
            foreach (var doc in duplicate) issues.Add(new("Error", "DUPLICATE_CREDIT_NOTE", $"Credit note number {duplicate.Key} is used more than once.", doc.Kind, doc.Id));
        foreach (var duplicate in valid.Where(x => x.Kind == "PurchaseInvoice").GroupBy(x => GstPreparationRules.Key(x.TaxDetails!.CounterpartyGstin ?? "", x.TaxDetails.DocumentNumber, x.Date)).Where(g => g.Count() > 1))
            foreach (var doc in duplicate) issues.Add(new("Error", "DUPLICATE_PURCHASE_KEY", "Multiple book invoices match the same supplier GSTIN, number and date. ITC is excluded until the duplicate is resolved.", doc.Kind, doc.Id));
        var statement = workspace?.StatementJson is null ? new List<Gst2BEntry>() : JsonSerializer.Deserialize<List<Gst2BEntry>>(workspace.StatementJson, Json)!;
        var reconciliation = Reconcile(valid.Where(x => x.Kind == "PurchaseInvoice").ToList(), statement);
        foreach (var purchase in valid.Where(x => x.Kind == "PurchaseInvoice" && x.TaxDetails!.ItcEligible))
        {
            var t = purchase.TaxDetails!; var key = GstPreparationRules.Key(t.CounterpartyGstin ?? "", t.DocumentNumber, t.DocumentDate);
            if (!reconciliation.Any(x => GstPreparationRules.Key(x.SupplierGstin, x.InvoiceNumber, x.InvoiceDate) == key && x.Status == "Matched / ITC available"))
                issues.Add(new("Error", "ITC_UNRECONCILED", "ITC was selected but the invoice is not exactly matched to available GSTR-2B tax heads. Import/reconcile the statement or exclude ITC after accountant review.", purchase.Kind, purchase.Id));
        }
        if (statement.Count > 0) issues.Add(new("Warning", "NORMALIZED_2B", "Reconciliation uses a normalized statement import, not a live GST Portal connection. Matching alone does not establish statutory ITC eligibility."));
        var outward = valid.Where(x => x.Kind != "PurchaseInvoice" && !issues.Any(i => i.Severity == "Error" && i.SourceKind == x.Kind && i.SourceId == x.Id)).ToList();
        decimal Sum(Func<GstTaxDetails, decimal> amount) => outward.Sum(x => (GstPreparationRules.IsCredit(x.Kind) ? -1m : 1m) * amount(x.TaxDetails!));
        var fingerprint = Hash(new { RulesVersion = "2026-10-21-v1", year, month, Gstin = settings?.Gstin, GstEnabled = settings?.GstEnabled, Documents = documents, Statement = statement, Issues = issues });
        var review = workspace?.CurrentReviewId is null ? null : await db.GstReturnReviews.AsNoTracking().SingleOrDefaultAsync(x => x.Id == workspace.CurrentReviewId, ct);
        return new(year, month, settings?.Gstin, workspace?.Status ?? "Draft", workspace?.Revision, fingerprint, workspace?.CurrentReviewId,
            workspace?.Status == "Reviewed" && (review is null || review.Fingerprint != fingerprint), documents, issues, reconciliation,
            Sum(x => x.TaxableValue), Sum(x => x.Cgst), Sum(x => x.Sgst), Sum(x => x.Igst), Sum(x => x.Cess), reconciliation.Sum(x => x.EligibleMatchedItc), DateTime.UtcNow)
        { ReviewedBy = review?.ReviewedBy, ReviewedAtUtc = review?.ReviewedAtUtc };
    }

    private static List<GstReconciliationRow> Reconcile(List<GstSourceDocument> purchases, List<Gst2BEntry> statement)
    {
        var output = new List<GstReconciliationRow>(); var used = new HashSet<string>();
        foreach (var p in purchases)
        {
            var t = p.TaxDetails!; var key = GstPreparationRules.Key(t.CounterpartyGstin ?? "", t.DocumentNumber, t.DocumentDate);
            var duplicate = purchases.Count(x => GstPreparationRules.Key(x.TaxDetails!.CounterpartyGstin ?? "", x.TaxDetails.DocumentNumber, x.Date) == key) > 1;
            var row = statement.SingleOrDefault(x => GstPreparationRules.Key(x.SupplierGstin, x.InvoiceNumber, x.InvoiceDate) == key);
            var matches = row is not null && row.TaxableValue == t.TaxableValue && row.Cgst == t.Cgst && row.Sgst == t.Sgst && row.Igst == t.Igst && row.Cess == t.Cess;
            var status = duplicate ? "Duplicate book key / ITC excluded" : row is null ? "Missing in 2B" : !matches ? "Tax mismatch" : !row.ItcAvailable ? "Matched / ITC unavailable" : "Matched / ITC available";
            used.Add(key); output.Add(new(t.CounterpartyGstin ?? "", t.DocumentNumber, t.DocumentDate, status, GstPreparationRules.Tax(t), row is null ? 0 : row.Cgst + row.Sgst + row.Igst + row.Cess,
                !duplicate && matches && row!.ItcAvailable && t.ItcEligible ? GstPreparationRules.Tax(t) : 0m));
        }
        foreach (var row in statement.Where(x => !used.Contains(GstPreparationRules.Key(x.SupplierGstin, x.InvoiceNumber, x.InvoiceDate))))
            output.Add(new(row.SupplierGstin, row.InvoiceNumber, row.InvoiceDate, "2B only / no current-period book match", 0m, row.Cgst + row.Sgst + row.Igst + row.Cess, 0m));
        return output;
    }

    private async Task<GstReturnWorkspace> Lock(int year, int month, CancellationToken ct)
    {
        _ = GstPreparationRules.Period(year, month);
        var workspace = await db.GstReturnWorkspaces.FromSqlInterpolated($"SELECT * FROM dbo.GstReturnWorkspaces WITH (UPDLOCK, HOLDLOCK) WHERE [Year] = {year} AND [Month] = {month}").SingleOrDefaultAsync(ct);
        if (workspace is not null) return workspace;
        workspace = new GstReturnWorkspace { Year = year, Month = month }; db.GstReturnWorkspaces.Add(workspace); return workspace;
    }
    private static void Editable(GstReturnWorkspace w) { if (w.Status != "Draft") throw new InvalidOperationException("Reviewed worksheets are locked. Reopen with a reason before editing tax details or replacing the statement."); }

    /// <summary>
    /// Saves audited tax annotations with revision checks while preserving original operational invoice amounts.
    /// </summary>
    public async Task SaveDetailsAsync(int year, int month, string kind, long id, SaveGstTaxDetails request, string actor, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var w = await Lock(year, month, ct); Editable(w);
        var preparation = await GetAsync(year, month, ct);
        var source = preparation.Documents.SingleOrDefault(x => x.Kind == kind && x.Id == id) ?? throw new KeyNotFoundException("Source document not found in this branch and period.");
        GstPreparationRules.Validate(source, request.Details, preparation.Gstin);
        var saved = await db.GstDocumentDetails.SingleOrDefaultAsync(x => x.SourceKind == kind && x.SourceId == id, ct);
        if (saved?.Revision != request.Revision) throw new InvalidOperationException("Tax details changed. Reload before saving.");
        if (saved is null) { saved = new GstDocumentDetails { SourceKind = kind, SourceId = id }; db.GstDocumentDetails.Add(saved); }
        var old = saved.DetailsJson; saved.DetailsJson = JsonSerializer.Serialize(request.Details, Json); saved.Revision = Guid.NewGuid(); saved.UpdatedAtUtc = DateTime.UtcNow; saved.UpdatedBy = actor;
        w.Revision = Guid.NewGuid();
        await audit.WriteAsync("GstTaxDetailsSaved", "GstDocumentDetails", $"{kind}:{id}", oldValues: old, newValues: request.Details, saveImmediately: false, cancellationToken: ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    /// <summary>
    /// Parses bounded normalized statement CSV and rejects malformed rows and duplicate invoice keys before a draft import can replace existing data.
    /// </summary>
    public static List<Gst2BEntry> ParseStatement(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv) || csv.Length > 1_000_000) throw new ArgumentException("Upload a normalized CSV statement of at most 1 MB.");
        using var parser = new TextFieldParser(new StringReader(csv.TrimStart('\uFEFF'))) { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true };
        parser.SetDelimiters(",");
        string[]? header;
        try { header = parser.ReadFields(); } catch (MalformedLineException) { throw new ArgumentException("Malformed CSV header. Nothing was imported."); }
        string[] expected = ["SupplierGstin", "InvoiceNumber", "InvoiceDate", "TaxableValue", "Cgst", "Sgst", "Igst", "Cess", "ItcAvailable"];
        if (header is null || !header.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase)) throw new ArgumentException("CSV columns must be SupplierGstin,InvoiceNumber,InvoiceDate,TaxableValue,Cgst,Sgst,Igst,Cess,ItcAvailable.");
        var rows = new List<Gst2BEntry>(); var keys = new HashSet<string>();
        try
        {
            while (!parser.EndOfData)
            {
                var fields = parser.ReadFields()!; if (fields.Length != 9 || rows.Count >= 5000) throw new ArgumentException("CSV requires nine columns and at most 5,000 rows.");
                var f = fields.Select(x => x.Trim()).ToArray();
                if (!GstPreparationRules.GstinValid(f[0].ToUpperInvariant()) || string.IsNullOrWhiteSpace(f[1]) || f[1].Length > 100 ||
                    !DateOnly.TryParseExact(f[2], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) || !bool.TryParse(f[8], out var available)) throw new ArgumentException("Invalid GSTIN, invoice, date or ItcAvailable flag in CSV.");
                var values = f.Skip(3).Take(5).Select(x => decimal.TryParse(x, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var n) && n >= 0 && n <= 999999999999m && decimal.Round(n, 2) == n ? n : throw new ArgumentException("CSV amounts must be non-negative decimals with at most two places.")).ToArray();
                if (!keys.Add(GstPreparationRules.Key(f[0], f[1], date))) throw new ArgumentException("Duplicate supplier/GSTIN/date invoice key in CSV. Nothing was imported.");
                rows.Add(new(f[0].ToUpperInvariant(), f[1], date, values[0], values[1], values[2], values[3], values[4], available));
            }
        }
        catch (MalformedLineException) { throw new ArgumentException("Malformed quoted CSV. Nothing was imported."); }
        if (rows.Count == 0) throw new ArgumentException("The statement has no data rows.");
        return rows;
    }

    /// <summary>
    /// Validates normalized statement data and atomically replaces the editable period&apos;s prior import using its expected revision.
    /// </summary>
    public async Task ImportAsync(int year, int month, string csv, Guid? revision, string actor, CancellationToken ct = default)
    {
        var rows = ParseStatement(csv); var (_, last) = GstPreparationRules.Period(year, month);
        if (rows.Any(x => x.InvoiceDate > last)) throw new ArgumentException("Statement invoices cannot be dated after the selected return period.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); var w = await Lock(year, month, ct); Editable(w);
        if (w.Id != 0 && w.Revision != revision || w.Id == 0 && revision is not null) throw new InvalidOperationException("Worksheet changed. Reload before importing.");
        w.StatementJson = JsonSerializer.Serialize(rows, Json); w.Revision = Guid.NewGuid();
        await audit.WriteAsync("Gst2BImported", "GstReturnWorkspace", $"{year}-{month:00}", newValues: new { Rows = rows.Count, Hash = Hash(rows), Actor = actor }, saveImmediately: false, cancellationToken: ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    /// <summary>
    /// Locks worksheet annotation edits after completeness validation and saves an audited source fingerprint; operational posting remains available.
    /// </summary>
    public async Task<GstPreparationDto> ReviewAsync(int year, int month, GstReviewRequest request, string actor, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); var w = await Lock(year, month, ct); Editable(w);
        var p = await GetAsync(year, month, ct);
        if (request.Fingerprint != p.Fingerprint || request.Revision != p.Revision) throw new InvalidOperationException("Sources changed. Regenerate and review the latest preparation.");
        if (p.Issues.Any(x => x.Severity == "Error")) throw new InvalidOperationException("Resolve every blocking validation issue before reviewing the worksheet.");
        var review = new GstReturnReview { Year = year, Month = month, Fingerprint = p.Fingerprint, SnapshotJson = JsonSerializer.Serialize(p, Json), ReviewedBy = actor, ReviewedAtUtc = DateTime.UtcNow };
        db.GstReturnReviews.Add(review); await db.SaveChangesAsync(ct);
        w.Status = "Reviewed"; w.CurrentReviewId = review.Id; w.Revision = Guid.NewGuid();
        review.SnapshotJson = JsonSerializer.Serialize(p with { Status = "Reviewed", ReviewId = review.Id, Revision = w.Revision, ReviewedBy = actor, ReviewedAtUtc = review.ReviewedAtUtc }, Json);
        await audit.WriteAsync("GstWorksheetReviewed", "GstReturnReview", review.Id.ToString(), newValues: new { year, month, p.Fingerprint, Actor = actor }, saveImmediately: false, cancellationToken: ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await GetAsync(year, month, ct);
    }
    /// <summary>
    /// Reopens the reviewed worksheet with an audited reason and expected revision, preserving historical review snapshots.
    /// </summary>
    public async Task ReopenAsync(int year, int month, GstReopenRequest request, string actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500) throw new ArgumentException("A reopen reason of up to 500 characters is required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); var w = await Lock(year, month, ct);
        if (w.Status != "Reviewed" || w.Revision != request.Revision) throw new InvalidOperationException("Worksheet changed or is not reviewed. Reload first.");
        w.Status = "Draft"; w.Revision = Guid.NewGuid();
        await audit.WriteAsync("GstWorksheetReopened", "GstReturnWorkspace", w.Id.ToString(), newValues: new { request.Reason, Actor = actor, PriorReviewId = w.CurrentReviewId }, saveImmediately: false, cancellationToken: ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}

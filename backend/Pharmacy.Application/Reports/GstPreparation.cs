using System.Globalization;
using System.Text.RegularExpressions;

namespace Pharmacy.Application.Reports;

/// <summary>
/// Explicit document tax facts transcribed from actual invoices or credit notes, separately from immutable operational amounts.
/// </summary>
public sealed record GstTaxDetails(string RegistrationGstin, string? CounterpartyGstin, string PlaceOfSupply,
    string SupplyCategory, string DocumentNumber, DateOnly DocumentDate, decimal GrossAmount,
    decimal TaxableValue, decimal Cgst, decimal Sgst, decimal Igst, decimal Cess, bool ItcEligible = false);
/// <summary>
/// Tax annotation input with the expected prior revision for optimistic concurrency checks.
/// </summary>
public sealed record SaveGstTaxDetails(GstTaxDetails Details, Guid? Revision);
/// <summary>
/// A GST worksheet source linked to original operational values and optional separately versioned tax annotations.
/// </summary>
public sealed record GstSourceDocument(string Kind, long Id, DateOnly Date, string Number, string Counterparty,
    decimal BookAmount, decimal? RecordedTaxable, decimal? RecordedGst, bool MissingHsn, bool MixedZeroTax,
    GstTaxDetails? TaxDetails = null, Guid? Revision = null, string? RelatedInvoice = null);
/// <summary>
/// One normalized imported supplier statement invoice used for exact document-and-tax reconciliation.
/// </summary>
public sealed record Gst2BEntry(string SupplierGstin, string InvoiceNumber, DateOnly InvoiceDate,
    decimal TaxableValue, decimal Cgst, decimal Sgst, decimal Igst, decimal Cess, bool ItcAvailable);
/// <summary>
/// Book-to-statement comparison and matched eligible ITC for a supplier invoice; a match is not full statutory eligibility proof.
/// </summary>
public sealed record GstReconciliationRow(string SupplierGstin, string InvoiceNumber, DateOnly InvoiceDate,
    string Status, decimal BookTax, decimal StatementTax, decimal EligibleMatchedItc);
/// <summary>
/// A completeness or validation issue optionally linked to its source document for accountant correction.
/// </summary>
public sealed record GstIssue(string Severity, string Code, string Message, string? SourceKind = null, long? SourceId = null);
/// <summary>
/// The current branch-period worksheet, validation, reconciliation and review fingerprint; reviewed status does not mean filed.
/// </summary>
public sealed record GstPreparationDto(int Year, int Month, string? Gstin, string Status, Guid? Revision,
    string Fingerprint, long? ReviewId, bool ChangedSinceReview, IReadOnlyList<GstSourceDocument> Documents,
    IReadOnlyList<GstIssue> Issues, IReadOnlyList<GstReconciliationRow> Reconciliation,
    decimal NetOutwardValue, decimal NetCgst, decimal NetSgst, decimal NetIgst, decimal NetCess,
    decimal MatchedEligibleItc, DateTime GeneratedAtUtc)
{
    public string? ReviewedBy { get; init; }
    public DateTime? ReviewedAtUtc { get; init; }
}
/// <summary>
/// The expected worksheet revision and source fingerprint the reviewer confirms before locking annotation edits.
/// </summary>
public sealed record GstReviewRequest(string Fingerprint, Guid? Revision);
/// <summary>
/// An audited reason and expected revision for reopening a reviewed worksheet.
/// </summary>
public sealed record GstReopenRequest(string Reason, Guid Revision);

/// <summary>
/// Defines versioned branch-period GST annotations, statement reconciliation and internal review operations.
/// </summary>
public interface IGstPreparationRepository
{
    /// <summary>
    /// Loads the branch-period source register, validation and reconciliation and detects changes against the last reviewed fingerprint.
    /// </summary>
    Task<GstPreparationDto> GetAsync(int year, int month, CancellationToken ct = default);
    /// <summary>
    /// Saves audited tax annotations with revision checks while preserving original operational invoice amounts.
    /// </summary>
    Task SaveDetailsAsync(int year, int month, string kind, long id, SaveGstTaxDetails request, string actor, CancellationToken ct = default);
    /// <summary>
    /// Validates normalized statement data and atomically replaces the editable period&apos;s prior import using its expected revision.
    /// </summary>
    Task ImportAsync(int year, int month, string csv, Guid? revision, string actor, CancellationToken ct = default);
    /// <summary>
    /// Locks worksheet annotation edits after completeness validation and saves an audited source fingerprint; operational posting remains available.
    /// </summary>
    Task<GstPreparationDto> ReviewAsync(int year, int month, GstReviewRequest request, string actor, CancellationToken ct = default);
    /// <summary>
    /// Reopens the reviewed worksheet with an audited reason and expected revision, preserving historical review snapshots.
    /// </summary>
    Task ReopenAsync(int year, int month, GstReopenRequest request, string actor, CancellationToken ct = default);
}

/// <summary>
/// Validates supported domestic tax annotations against original document snapshots without inferring missing statutory facts.
/// </summary>
public static class GstPreparationRules
{
    /// <summary>
    /// Validates a supported year/month and returns the inclusive calendar-month boundaries for the worksheet.
    /// </summary>
    public static (DateOnly Start, DateOnly End) Period(int year, int month)
    {
        if (year < 2017 || year > DateTime.UtcNow.Year + 1 || month is < 1 or > 12) throw new ArgumentException("Select a valid GST month and year.");
        var start = new DateOnly(year, month, 1); return (start, start.AddMonths(1).AddDays(-1));
    }
    /// <summary>
    /// Checks the supported GSTIN text pattern only; this is not portal registration or checksum verification.
    /// </summary>
    public static bool GstinValid(string? value) => value is not null && Regex.IsMatch(value, @"\A[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]\z");
    /// <summary>
    /// Normalizes supplier GSTIN, invoice number and date into an exact-match reconciliation key.
    /// </summary>
    public static string Key(string gstin, string number, DateOnly date) => $"{gstin.Trim().ToUpperInvariant()}|{number.Trim().ToUpperInvariant()}|{date:yyyy-MM-dd}";
    /// <summary>
    /// Sums the explicitly annotated CGST, SGST/UTGST, IGST and cess amounts without inferring missing tax heads.
    /// </summary>
    public static decimal Tax(GstTaxDetails t) => t.Cgst + t.Sgst + t.Igst + t.Cess;
    /// <summary>
    /// Identifies operational return and cancellation source kinds whose credits are represented separately from original invoices.
    /// </summary>
    public static bool IsCredit(string kind) => kind is "SalesReturn" or "SaleCancellation";
    /// <summary>
    /// Validates explicit domestic tax facts against the source document, registration and original snapshots; unsupported cases require accountant handling.
    /// </summary>
    public static void Validate(GstSourceDocument source, GstTaxDetails t, string? registration)
    {
        if (!GstinValid(t.RegistrationGstin) || t.RegistrationGstin != registration) throw new ArgumentException("Pharmacy GSTIN must match this branch's configured GST registration. Historical registration changes need accountant handling.");
        if (!string.IsNullOrWhiteSpace(t.CounterpartyGstin) && !GstinValid(t.CounterpartyGstin)) throw new ArgumentException("Counterparty GSTIN format is invalid.");
        if (!Regex.IsMatch(t.PlaceOfSupply ?? "", @"\A[0-9]{2}\z") || !int.TryParse(t.PlaceOfSupply, out var state) || state is < 1 or > 38)
            throw new ArgumentException("Enter a supported domestic place-of-supply state code (01–38).");
        if (t.SupplyCategory is not ("Taxable" or "NilRated" or "Exempt" or "NonGst")) throw new ArgumentException("Select the supply category explicitly.");
        if (!Regex.IsMatch(t.DocumentNumber ?? "", @"\A[A-Za-z0-9/-]{1,16}\z")) throw new ArgumentException("GST document numbers must contain 1–16 letters, digits, / or -.");
        if (t.DocumentDate != source.Date) throw new ArgumentException("This worksheet supports documents issued on the recorded event date only. Different credit-note dates require accountant handling.");
        if (!IsCredit(source.Kind) && t.DocumentNumber != source.Number) throw new ArgumentException("Original invoice numbers cannot be changed through tax annotations.");
        var amounts = new[] { t.GrossAmount, t.TaxableValue, t.Cgst, t.Sgst, t.Igst, t.Cess };
        if (amounts.Any(x => x < 0 || x > 999999999999m || decimal.Round(x, 2) != x)) throw new ArgumentException("Amounts must be non-negative with at most two decimal places.");
        if (t.GrossAmount != t.TaxableValue + Tax(t)) throw new ArgumentException("Supply value plus tax heads must equal document gross amount.");
        if (source.Kind == "PurchaseInvoice" && source.BookAmount > 0 && t.GrossAmount <= 0) throw new ArgumentException("Enter the actual positive supplier document gross amount; stock cost is not a substitute for purchase tax facts.");
        if (source.Kind != "PurchaseInvoice" && (t.GrossAmount != source.BookAmount || t.TaxableValue != source.RecordedTaxable || Tax(t) != source.RecordedGst || t.Cess != 0))
            throw new ArgumentException("Sales and credit tax annotations must reconcile exactly to original invoice tax snapshots. They cannot rewrite amounts.");
        if (t.SupplyCategory != "Taxable" && Tax(t) != 0) throw new ArgumentException("Nil, exempt and non-GST supplies cannot carry tax in this worksheet.");
        if (source.MixedZeroTax) throw new ArgumentException("Mixed zero-tax and taxed invoice lines require line-level classification outside this worksheet.");
        var supplierState = source.Kind == "PurchaseInvoice" ? (string.IsNullOrWhiteSpace(t.CounterpartyGstin) ? null : t.CounterpartyGstin[..2]) : t.RegistrationGstin[..2];
        if (Tax(t) > 0 && supplierState is null) throw new ArgumentException("A supplier GSTIN is required for a taxed purchase.");
        if (Tax(t) > 0 && supplierState == t.PlaceOfSupply && (t.Igst != 0 || Math.Abs(t.Cgst - t.Sgst) > .01m))
            throw new ArgumentException("Domestic intra-state tax must use CGST and SGST/UTGST, with matching rounded amounts.");
        if (Tax(t) > 0 && supplierState != t.PlaceOfSupply && (t.Cgst != 0 || t.Sgst != 0)) throw new ArgumentException("Inter-state tax must use IGST, without CGST or SGST/UTGST.");
        if (source.Kind != "PurchaseInvoice" && t.ItcEligible) throw new ArgumentException("ITC eligibility applies only to purchase invoices.");
    }
}

/// <summary>
/// Builds internal GST review exports and rejects stale reviewed worksheets; it does not file returns or pay tax.
/// </summary>
public sealed class GstPreparationService(IGstPreparationRepository repository, IReportExporter exporter, IAdvancedReportRepository advanced)
{
    /// <summary>
    /// Exports supported worksheet registers and rejects stale reviewed data; the download is internal accountant preparation, not a portal filing payload.
    /// </summary>
    public async Task<ReportDownload> ExportAsync(int year, int month, string format, CancellationToken ct = default)
    {
        if (format is not ("xlsx" or "pdf")) throw new ArgumentException("Choose xlsx or pdf.");
        var p = await repository.GetAsync(year, month, ct);
        if (p.Status == "Reviewed" && p.ChangedSinceReview) throw new InvalidOperationException("Sources changed after review. Reopen and review the worksheet before exporting it.");
        var (start, end) = GstPreparationRules.Period(year, month);
        var valid = p.Documents.Where(x => x.TaxDetails is not null && !p.Issues.Any(i => i.Severity == "Error" && i.SourceKind == x.Kind && i.SourceId == x.Id)).ToList();
        ReportTable Documents(string title, IEnumerable<GstSourceDocument> rows) => new(title,
            ["Source", "ID", "Document", "Related invoice", "Date", "Counterparty", "Counterparty GSTIN", "POS", "Category", "Gross", "Supply value", "CGST", "SGST/UTGST", "IGST", "Cess", "ITC selected"],
            rows.Select(x => (IReadOnlyList<object?>)new object?[] { x.Kind, x.Id, x.TaxDetails!.DocumentNumber, x.RelatedInvoice, x.Date.ToString("yyyy-MM-dd"), x.Counterparty, x.TaxDetails.CounterpartyGstin, x.TaxDetails.PlaceOfSupply, x.TaxDetails.SupplyCategory,
                x.TaxDetails.GrossAmount, x.TaxDetails.TaxableValue, x.TaxDetails.Cgst, x.TaxDetails.Sgst, x.TaxDetails.Igst, x.TaxDetails.Cess, x.TaxDetails.ItcEligible ? "Yes" : "No" }).ToList());
        var tables = new List<ReportTable> {
            new("Review status", ["Field", "Value"], [["Status", p.Status], ["GSTIN", p.Gstin], ["Scope", "Active branch only; consolidate all branches of the GSTIN before filing"], ["Fingerprint", p.Fingerprint], ["Review ID", p.ReviewId], ["Reviewed by", p.ReviewedBy], ["Reviewed at UTC", p.ReviewedAtUtc?.ToString("O")]]),
            Documents("GSTR-1 working register — registered recipients", valid.Where(x => x.Kind == "SalesInvoice" && !string.IsNullOrWhiteSpace(x.TaxDetails!.CounterpartyGstin))),
            Documents("GSTR-1 working register — unregistered recipients (accountant table mapping required)", valid.Where(x => x.Kind == "SalesInvoice" && string.IsNullOrWhiteSpace(x.TaxDetails!.CounterpartyGstin))),
            Documents("Credit-note working register", valid.Where(x => GstPreparationRules.IsCredit(x.Kind))),
            Documents("Purchase tax facts — transcribed from supplier documents", valid.Where(x => x.Kind == "PurchaseInvoice")),
            new("GSTR-3B preparation totals — not a filing or payable calculation", ["Metric", "INR"], [["Net outward supply value", p.NetOutwardValue], ["Net CGST", p.NetCgst], ["Net SGST/UTGST", p.NetSgst], ["Net IGST", p.NetIgst], ["Net cess", p.NetCess], ["Matched eligible ITC — review required", p.MatchedEligibleItc]]),
            new("GSTR-2B reconciliation", ["Supplier GSTIN", "Invoice", "Date", "Result", "Book tax", "2B tax", "Matched eligible ITC"], p.Reconciliation.Select(x => (IReadOnlyList<object?>)new object?[] { x.SupplierGstin, x.InvoiceNumber, x.InvoiceDate.ToString("yyyy-MM-dd"), x.Status, x.BookTax, x.StatementTax, x.EligibleMatchedItc }).ToList()),
            new("Validation issues", ["Severity", "Code", "Source", "ID", "Message"], p.Issues.Select(x => (IReadOnlyList<object?>)new object?[] { x.Severity, x.Code, x.SourceKind, x.SourceId, x.Message }).ToList()),
            new("All source documents — completeness register", ["Source", "ID", "Original number", "Event date", "Book amount", "Tax details saved"], p.Documents.Select(x => (IReadOnlyList<object?>)new object?[] { x.Kind, x.Id, x.Number, x.Date.ToString("yyyy-MM-dd"), x.BookAmount, x.TaxDetails is not null ? "Yes" : "No" }).ToList())
        };
        var outward = valid.Where(x => x.Kind != "PurchaseInvoice").ToList();
        tables.Add(new("Outward supply categories — accountant GSTR-3B mapping", ["Category", "Net supply value", "CGST", "SGST/UTGST", "IGST", "Cess"],
            outward.GroupBy(x => x.TaxDetails!.SupplyCategory).Select(g => (IReadOnlyList<object?>)new object?[] { g.Key,
                g.Sum(x => (GstPreparationRules.IsCredit(x.Kind) ? -1m : 1m) * x.TaxDetails!.TaxableValue),
                g.Sum(x => (GstPreparationRules.IsCredit(x.Kind) ? -1m : 1m) * x.TaxDetails!.Cgst),
                g.Sum(x => (GstPreparationRules.IsCredit(x.Kind) ? -1m : 1m) * x.TaxDetails!.Sgst),
                g.Sum(x => (GstPreparationRules.IsCredit(x.Kind) ? -1m : 1m) * x.TaxDetails!.Igst),
                g.Sum(x => (GstPreparationRules.IsCredit(x.Kind) ? -1m : 1m) * x.TaxDetails!.Cess) }).ToList()));
        var hsn = await advanced.GetAsync("gst", start, end, ct);
        tables.AddRange(hsn.Tables.Select(t => t with { Title = "Original HSN snapshots — " + t.Title }));
        return exporter.Export(new("gst-preparation", start, end,
            $"{p.Status.ToUpperInvariant()} — accountant review worksheet, not GST Portal upload JSON. Active branch only. Missing/invalid documents are excluded from annotated tax totals and remain in the completeness register. Original HSN snapshots include all recorded source activity, independent of annotation validation; UQC/turnover-specific HSN reporting requires accountant review. No return is filed, no ITC is claimed, and no tax payable is inferred. {p.Issues.Count(i => i.Severity == "Error")} blocking issue(s).", tables), format);
    }
}

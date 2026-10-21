namespace Pharmacy.Application.Reports;

/// <summary>
/// A presentation-independent report table shared by the web view and file exporters.
/// </summary>
public sealed record ReportTable(string Title, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<object?>> Rows);
/// <summary>
/// A report kind, business-date range, explanatory notes and tabular results ready for display or export.
/// </summary>
public sealed record AdvancedReportDto(string Kind, DateOnly StartDate, DateOnly EndDate, string Notes, IReadOnlyList<ReportTable> Tables);
/// <summary>
/// Generated file bytes with their MIME type and suggested download filename.
/// </summary>
public sealed record ReportDownload(byte[] Content, string ContentType, string FileName);

/// <summary>
/// Provides tabular sales, GST, medicine, gross-profit and current-stock reports for export and display.
/// </summary>
public interface IAdvancedReportRepository
{
    /// <summary>
    /// Builds the selected branch report with original invoice snapshots and dated credits; stock valuation is a current snapshot regardless of the selected historical period.
    /// </summary>
    Task<AdvancedReportDto> GetAsync(string kind, DateOnly start, DateOnly end, CancellationToken ct = default);
}
/// <summary>
/// Converts application report tables into downloadable files independently of database queries.
/// </summary>
public interface IReportExporter
{
    /// <summary>
    /// Converts report tables into the requested native Excel or PDF download without querying or changing business data.
    /// </summary>
    ReportDownload Export(AdvancedReportDto report, string format);
}
/// <summary>
/// Validates report kind, date range and export format before loading or exporting tabular analytics.
/// </summary>
public interface IAdvancedReportService
{
    /// <summary>
    /// Validates supported report kind and business-date range before querying tabular branch results.
    /// </summary>
    Task<AdvancedReportDto> GetAsync(string kind, DateOnly start, DateOnly end, CancellationToken ct = default);
    /// <summary>
    /// Validates Excel/PDF format and exports the same report tables used by the application view.
    /// </summary>
    Task<ReportDownload> ExportAsync(string kind, DateOnly start, DateOnly end, string format, CancellationToken ct = default);
}
/// <summary>
/// Coordinates bounded report queries and Excel/PDF generation using application-layer contracts.
/// </summary>
public sealed class AdvancedReportService(IAdvancedReportRepository repository, IReportExporter exporter) : IAdvancedReportService
{
    public static readonly string[] Kinds = ["sales", "gst", "medicines", "profit", "stock"];
    /// <summary>
    /// Validates supported report kind and business-date range before querying tabular branch results.
    /// </summary>
    public Task<AdvancedReportDto> GetAsync(string kind, DateOnly start, DateOnly end, CancellationToken ct = default)
    {
        _ = BusinessDates.Range(start, end);
        if (!Kinds.Contains(kind)) throw new ArgumentException("Unknown report type.");
        return repository.GetAsync(kind, start, end, ct);
    }
    /// <summary>
    /// Validates Excel/PDF format and exports the same report tables used by the application view.
    /// </summary>
    public async Task<ReportDownload> ExportAsync(string kind, DateOnly start, DateOnly end, string format, CancellationToken ct = default)
    {
        if (format is not ("xlsx" or "pdf")) throw new ArgumentException("Choose xlsx or pdf.");
        return exporter.Export(await GetAsync(kind, start, end, ct), format);
    }
}

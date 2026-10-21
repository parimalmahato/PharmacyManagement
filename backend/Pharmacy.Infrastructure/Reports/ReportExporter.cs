using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using Pharmacy.Application.Reports;

namespace Pharmacy.Infrastructure.Reports;

/// <summary>
/// Produces native Excel and PDF files from validated report tables without altering report source records.
/// </summary>
public sealed class ReportExporter : IReportExporter
{
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    static ReportExporter()
    {
        if (GlobalFontSettings.FontResolver is null) GlobalFontSettings.FontResolver = new ReportFonts();
    }
    /// <summary>
    /// Converts report tables into the requested native Excel or PDF download without querying or changing business data.
    /// </summary>
    public ReportDownload Export(AdvancedReportDto report, string format)
    {
        if (format is not ("xlsx" or "pdf")) throw new ArgumentException("Choose xlsx or pdf.");
        if (report.Tables.Sum(x => x.Rows.Count) > 50000) throw new ArgumentException("Report exceeds 50,000 rows. Choose a shorter date range.");
        return new(format == "xlsx" ? Excel(report) : Pdf(report),
            format == "xlsx" ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" : "application/pdf",
            $"pharmacy-{report.Kind}-{report.StartDate:yyyy-MM-dd}-{report.EndDate:yyyy-MM-dd}.{format}");
    }

    private static byte[] Excel(AdvancedReportDto report)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            XNamespace ct = "http://schemas.openxmlformats.org/package/2006/content-types";
            XNamespace rel = "http://schemas.openxmlformats.org/package/2006/relationships";
            XNamespace docRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            Write(zip, "[Content_Types].xml", new XElement(ct + "Types",
                new XElement(ct + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(ct + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                new XElement(ct + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                report.Tables.Select((_, i) => new XElement(ct + "Override", new XAttribute("PartName", $"/xl/worksheets/sheet{i + 1}.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")))));
            Write(zip, "_rels/.rels", new XElement(rel + "Relationships", new XElement(rel + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Type", docRel.NamespaceName + "/officeDocument"), new XAttribute("Target", "xl/workbook.xml"))));
            Write(zip, "xl/workbook.xml", new XElement(S + "workbook", new XAttribute(XNamespace.Xmlns + "r", docRel), new XElement(S + "sheets",
                report.Tables.Select((_, i) => new XElement(S + "sheet", new XAttribute("name", $"Report {i + 1}"), new XAttribute("sheetId", i + 1), new XAttribute(docRel + "id", $"rId{i + 1}"))))));
            Write(zip, "xl/_rels/workbook.xml.rels", new XElement(rel + "Relationships", report.Tables.Select((_, i) => new XElement(rel + "Relationship",
                new XAttribute("Id", $"rId{i + 1}"), new XAttribute("Type", docRel.NamespaceName + "/worksheet"), new XAttribute("Target", $"worksheets/sheet{i + 1}.xml")))));
            for (var i = 0; i < report.Tables.Count; i++)
            {
                var table = report.Tables[i];
                var rows = new List<IReadOnlyList<object?>> {
                    new object?[] { $"Pharmacy {report.Kind} report - {table.Title}" },
                    new object?[] { $"{report.StartDate:yyyy-MM-dd} to {report.EndDate:yyyy-MM-dd} | IST | INR" },
                    new object?[] { report.Notes }, Array.Empty<object?>(), table.Columns.Cast<object?>().ToArray()
                };
                rows.AddRange(table.Rows);
                Write(zip, $"xl/worksheets/sheet{i + 1}.xml", new XElement(S + "worksheet",
                    new XElement(S + "sheetViews", new XElement(S + "sheetView", new XAttribute("workbookViewId", 0), new XElement(S + "pane", new XAttribute("ySplit", 5), new XAttribute("topLeftCell", "A6"), new XAttribute("activePane", "bottomLeft"), new XAttribute("state", "frozen")))),
                    new XElement(S + "cols", new XElement(S + "col", new XAttribute("min", 1), new XAttribute("max", Math.Max(1, table.Columns.Count)), new XAttribute("width", 23), new XAttribute("customWidth", 1))),
                    new XElement(S + "sheetData", rows.Select((row, n) => new XElement(S + "row", new XAttribute("r", n + 1), row.Select((value, c) => Cell(value, $"{Column(c)}{n + 1}"))))),
                    new XElement(S + "autoFilter", new XAttribute("ref", $"A5:{Column(Math.Max(0, table.Columns.Count - 1))}{rows.Count}"))));
            }
        }
        return stream.ToArray();
    }
    private static XElement Cell(object? value, string address)
    {
        var numeric = value is decimal or int or long or double or float;
        return new XElement(S + "c", new XAttribute("r", address), new XAttribute("t", numeric ? "n" : "inlineStr"),
            numeric ? new XElement(S + "v", Convert.ToString(value, CultureInfo.InvariantCulture)) :
                new XElement(S + "is", new XElement(S + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), Convert.ToString(value, CultureInfo.InvariantCulture) ?? "")));
    }
    private static string Column(int index)
    {
        var name = "";
        for (index++; index > 0; index = (index - 1) / 26) name = (char)('A' + (index - 1) % 26) + name;
        return name;
    }
    private static void Write(ZipArchive zip, string path, XElement root)
    {
        using var entry = zip.CreateEntry(path).Open();
        new XDocument(root).Save(entry);
    }

    private static byte[] Pdf(AdvancedReportDto report)
    {
        using var document = new PdfDocument();
        document.Info.Title = $"Pharmacy {report.Kind} report";
        var font = new XFont("Report", 8);
        var heading = new XFont("Report", 12);
        foreach (var table in report.Tables)
        {
            XGraphics? graphics = null;
            double y = 0;
            const double width = 780;
            var cellWidth = width / Math.Max(1, table.Columns.Count);
            void NewPage()
            {
                graphics?.Dispose();
                var page = document.AddPage(); page.Width = XUnit.FromPoint(842); page.Height = XUnit.FromPoint(595);
                graphics = XGraphics.FromPdfPage(page);
                graphics.DrawString($"Pharmacy {report.Kind} - {table.Title}", heading, XBrushes.Black, 30, 30);
                graphics.DrawString($"{report.StartDate:yyyy-MM-dd} to {report.EndDate:yyyy-MM-dd} | IST | INR | Page {document.PageCount}", font, XBrushes.Black, 30, 46);
                y = 62;
                foreach (var line in Wrap(report.Notes, width, graphics, font)) { graphics.DrawString(line, font, XBrushes.Black, 30, y); y += 11; }
                y += 8;
                DrawRow(table.Columns.Cast<object?>().ToArray(), true);
            }
            void DrawRow(IReadOnlyList<object?> cells, bool header)
            {
                var text = cells.Select(value => Wrap(value is decimal amount ? amount.ToString("0.00", CultureInfo.InvariantCulture) : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "", cellWidth - 8, graphics!, font).ToList()).ToList();
                var height = Math.Max(1, text.Select(x => x.Count).DefaultIfEmpty(1).Max()) * 11 + 10;
                if (!header && y + height > 565) NewPage();
                for (var c = 0; c < cells.Count; c++)
                {
                    graphics!.DrawRectangle(header ? XBrushes.LightGray : XBrushes.White, 30 + c * cellWidth, y, cellWidth, height);
                    graphics.DrawRectangle(XPens.LightGray, 30 + c * cellWidth, y, cellWidth, height);
                    for (var l = 0; l < text[c].Count; l++) graphics.DrawString(text[c][l], font, XBrushes.Black, 34 + c * cellWidth, y + 12 + l * 11);
                }
                y += height;
            }
            NewPage();
            foreach (var row in table.Rows) DrawRow(row, false);
            if (!table.Rows.Any()) graphics!.DrawString("No records in this period.", font, XBrushes.Black, 30, y + 15);
            graphics?.Dispose();
        }
        using var output = new MemoryStream(); document.Save(output, false); return output.ToArray();
    }
    private static IEnumerable<string> Wrap(string text, double width, XGraphics graphics, XFont font)
    {
        var line = "";
        foreach (var character in text.Replace('\r', ' ').Replace('\n', ' '))
        {
            if (line.Length > 0 && graphics.MeasureString(line + character, font).Width > width) { yield return line; line = ""; }
            line += character;
        }
        if (line.Length > 0) yield return line;
    }
    private sealed class ReportFonts : IFontResolver
    {
        /// <summary>
        /// Maps report font requests to the exporter&apos;s configured TrueType face rather than relying on platform-specific family resolution.
        /// </summary>
        public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic) => new("Report");
        /// <summary>
        /// Loads a readable configured, packaged or supported system TrueType font and reports missing font deployment explicitly.
        /// </summary>
        public byte[] GetFont(string faceName)
        {
            var paths = new[] { Environment.GetEnvironmentVariable("PHARMACY_REPORT_FONT"),
                Path.Combine(AppContext.BaseDirectory, "Fonts", "Report.ttf"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", "arial.ttf"),
                "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/fonts/truetype/liberation2/LiberationSans-Regular.ttf" };
            var path = paths.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x) && File.Exists(x));
            return path is not null ? File.ReadAllBytes(path) : throw new InvalidOperationException("Configure PHARMACY_REPORT_FONT with a readable TrueType font for PDF exports.");
        }
    }
}

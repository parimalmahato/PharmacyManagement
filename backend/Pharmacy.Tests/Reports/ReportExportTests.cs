using System.IO.Compression;
using System.Xml.Linq;
using PdfSharp.Pdf.IO;
using Pharmacy.Application.Reports;
using Pharmacy.Infrastructure.Reports;

namespace Pharmacy.Tests.Reports;

public sealed class ReportExportTests
{
    private static AdvancedReportDto Report(int count = 1) => new("medicines", new(2026, 10, 1), new(2026, 10, 21), "IST. Amounts in INR.",
        [new("Medicine activity", ["Medicine", "Value"], Enumerable.Range(0, count).Select(_ => (IReadOnlyList<object?>)new object?[] { "=SUM(A1:A2) & Café", 123.45m }).ToList())]);

    [Fact]
    public void ExcelIsOpenXmlWithNumericCellsAndLiteralUntrustedText()
    {
        var file = new ReportExporter().Export(Report(), "xlsx");
        Assert.EndsWith(".xlsx", file.FileName);
        using var stream = new MemoryStream(file.Content);
        using var zip = new ZipArchive(stream);
        Assert.NotNull(zip.GetEntry("[Content_Types].xml"));
        Assert.NotNull(zip.GetEntry("xl/workbook.xml"));
        using var entry = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var xml = XDocument.Load(entry);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        Assert.Empty(xml.Descendants(ns + "f"));
        var number = xml.Descendants(ns + "c").Single(x => (string?)x.Attribute("r") == "B6");
        Assert.Equal("n", (string?)number.Attribute("t"));
        Assert.Equal("123.45", number.Element(ns + "v")!.Value);
        Assert.Contains("=SUM(A1:A2) & Café", xml.Descendants(ns + "t").Select(x => x.Value));
    }

    [Fact]
    public void PdfIsReadableAndPaginatesLargeTables()
    {
        var file = new ReportExporter().Export(Report(150), "pdf");
        Assert.Equal("application/pdf", file.ContentType);
        using var stream = new MemoryStream(file.Content);
        using var pdf = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        Assert.True(pdf.PageCount > 1);
        Assert.Equal("Pharmacy medicines report", pdf.Info.Title);
    }

    [Fact]
    public void InvalidFormatAndOversizedExportsAreRejected()
    {
        var exporter = new ReportExporter();
        Assert.Throws<ArgumentException>(() => exporter.Export(Report(), "csv"));
        Assert.Throws<ArgumentException>(() => exporter.Export(Report(50001), "xlsx"));
    }
}

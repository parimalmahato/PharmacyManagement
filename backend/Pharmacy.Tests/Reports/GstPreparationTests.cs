using Pharmacy.Application.Reports;
using Pharmacy.Infrastructure.Reports;

namespace Pharmacy.Tests.Reports;

public sealed class GstPreparationTests
{
    private const string Gstin = "27ABCDE1234F1Z5";
    private static GstSourceDocument Source() => new("SalesInvoice", 1, new DateOnly(2026, 10, 1), "INV-1", "Customer", 105m, 100m, 5m, false, false);
    private static GstTaxDetails Details() => new(Gstin, null, "27", "Taxable", "INV-1", new(2026, 10, 1), 105, 100, 2.5m, 2.5m, 0, 0);

    [Fact]
    public void RequiresExactOriginalTaxSnapshotsAndExplicitDomesticHeads()
    {
        GstPreparationRules.Validate(Source(), Details(), Gstin);
        GstPreparationRules.Validate(Source(), Details() with { PlaceOfSupply = "29", Cgst = 0, Sgst = 0, Igst = 5 }, Gstin);
        Assert.Throws<ArgumentException>(() => GstPreparationRules.Validate(Source(), Details() with { Cgst = 0, Sgst = 0, Igst = 5 }, Gstin));
        Assert.Throws<ArgumentException>(() => GstPreparationRules.Validate(Source(), Details() with { GrossAmount = 106, TaxableValue = 101 }, Gstin));
        Assert.Throws<ArgumentException>(() => GstPreparationRules.Validate(Source(), Details() with { RegistrationGstin = "29ABCDE1234F1Z5" }, Gstin));
        Assert.Throws<ArgumentException>(() => GstPreparationRules.Validate(Source(), Details() with { DocumentNumber = "ALTERED" }, Gstin));
        Assert.Throws<ArgumentException>(() => GstPreparationRules.Validate(Source(), Details() with { SupplyCategory = "Exempt" }, Gstin));
        Assert.Throws<ArgumentException>(() => GstPreparationRules.Validate(Source() with { MixedZeroTax = true }, Details(), Gstin));
    }

    [Fact]
    public void PurchaseTaxFactsAreIndependentOfStockCost_AndNeedSupplierIdentity()
    {
        var purchase = Source() with { Kind = "PurchaseInvoice", BookAmount = 70, RecordedTaxable = null, RecordedGst = null };
        GstPreparationRules.Validate(purchase, Details() with { CounterpartyGstin = "27FGHIJ5678K1Z5", ItcEligible = true }, Gstin);
        Assert.Throws<ArgumentException>(() => GstPreparationRules.Validate(purchase, Details() with { CounterpartyGstin = "" }, Gstin));
    }

    [Fact]
    public void CreditsRequireActualNoteNumbersAndRetainRecordedEventDate()
    {
        var credit = Source() with { Kind = "SalesReturn", Number = "Return #1" };
        GstPreparationRules.Validate(credit, Details() with { DocumentNumber = "CN-1" }, Gstin);
        Assert.Throws<ArgumentException>(() => GstPreparationRules.Validate(credit, Details() with { DocumentNumber = "" }, Gstin));
        Assert.Throws<ArgumentException>(() => GstPreparationRules.Validate(credit, Details() with { DocumentDate = new(2026, 10, 2) }, Gstin));
    }

    [Fact]
    public void ParsesQuotedNormalizedCsv_AndRejectsDuplicatesAndMalformedAmounts()
    {
        const string header = "SupplierGstin,InvoiceNumber,InvoiceDate,TaxableValue,Cgst,Sgst,Igst,Cess,ItcAvailable\r\n";
        var row = "27ABCDE1234F1Z5,\"INV,1\",2026-10-01,100.00,2.50,2.50,0,0,true\r\n";
        var parsed = Assert.Single(GstPreparationRepository.ParseStatement(header + row));
        Assert.Equal("INV,1", parsed.InvoiceNumber); Assert.True(parsed.ItcAvailable);
        Assert.Throws<ArgumentException>(() => GstPreparationRepository.ParseStatement(header + row + row));
        Assert.Throws<ArgumentException>(() => GstPreparationRepository.ParseStatement(header + row.Replace("100.00", "100.001")));
        Assert.Throws<ArgumentException>(() => GstPreparationRepository.ParseStatement(header + row.Replace("true", "maybe")));
        Assert.Throws<ArgumentException>(() => GstPreparationRepository.ParseStatement("wrong,header"));
        Assert.Throws<ArgumentException>(() => GstPreparationRepository.ParseStatement(header));
    }
}

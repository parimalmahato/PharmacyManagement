using System.Globalization;
using Microsoft.AspNetCore.WebUtilities;
using Pharmacy.Application.Payments;
using Pharmacy.Application.Sales;
using Pharmacy.Application.Settings;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Tests.Payments;

public sealed class UpiPaymentRequestTests
{
    private static SaleDto Sale() => new(1, "RX-123", DateTime.UtcNow, 100m, "Completed", [], null, null, null, 100m, 0m, 100m, 0m) { PaidAmount = 25m };
    private static PharmacySettingsDto Settings() => SettingsManagement.ToDto(new PharmacySettings {
        UpiEnabled = true, UpiId = "pharmacy@bank", UpiPayeeName = "Pharmacy & Sons", UpiMerchantCode = "5912"
    });

    [Fact]
    public void EncodesPayeeAndAmountInvariantly_WithoutMarkingInvoicePaid()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var sale = Sale();
            var result = UpiPaymentRequests.Create(sale, Settings(), 12.50m);
            var query = QueryHelpers.ParseQuery(new Uri(result.PaymentUri).Query);
            Assert.StartsWith("upi://pay?", result.PaymentUri);
            Assert.Equal("pharmacy@bank", query["pa"].ToString());
            Assert.Equal("Pharmacy & Sons", query["pn"].ToString());
            Assert.Equal("12.50", query["am"].ToString()); Assert.Equal("INR", query["cu"].ToString());
            Assert.Equal("Invoice RX-123", query["tn"].ToString()); Assert.Equal("5912", query["mc"].ToString());
            Assert.Equal(7, query.Count); Assert.Equal(75m, sale.BalanceAmount); Assert.Equal(25m, sale.PaidAmount);
            Assert.NotEqual(result.Reference, UpiPaymentRequests.Create(sale, Settings(), null).Reference);
            Assert.Equal(75m, UpiPaymentRequests.Create(sale, Settings(), null).Amount);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("75.01")]
    [InlineData("1.001")]
    public void RejectsInvalidAmounts(string value) => Assert.Throws<ArgumentException>(() => UpiPaymentRequests.Create(Sale(), Settings(), decimal.Parse(value, CultureInfo.InvariantCulture)));

    [Fact]
    public void BlocksDisabledCancelledAndSettledInvoices()
    {
        Assert.Throws<InvalidOperationException>(() => UpiPaymentRequests.Create(Sale(), Settings() with { UpiEnabled = false }, null));
        Assert.Throws<InvalidOperationException>(() => UpiPaymentRequests.Create(Sale() with { Status = "Cancelled" }, Settings(), null));
        Assert.Throws<InvalidOperationException>(() => UpiPaymentRequests.Create(Sale() with { PaidAmount = 100m }, Settings(), null));
    }

    [Theory]
    [InlineData(null, "Shop", null)]
    [InlineData("shop@bank", null, null)]
    [InlineData("shop@bank&am=1", "Shop", null)]
    [InlineData("upi://pay", "Shop", null)]
    [InlineData("shop@bank", "Shop\nOther", null)]
    [InlineData("shop@bank", "Shop", "123")]
    [InlineData("shop@bank", "Shop", "12x4")]
    public void RejectsMalformedConfiguration(string? vpa, string? name, string? code) =>
        Assert.Throws<ArgumentException>(() => UpiPaymentRequests.ValidateConfiguration(true, vpa, name, code));
}

using Pharmacy.Application.Settings;

namespace Pharmacy.Tests.Settings;

public sealed class SettingsTests
{
    private static UpdatePharmacySettingsRequest Valid() => new("Pharmacy", "Street", "9876543210", null, null, "SAL", false, 0);
    [Fact]
    public void DefaultConfigurationIsValid() => SettingsManagement.Validate(Valid());
    [Theory]
    [InlineData(0)]
    [InlineData(100001)]
    public void RejectsInvalidStockThresholds(int threshold) =>
        Assert.Throws<ArgumentException>(() => SettingsManagement.Validate(Valid() with { LowStockThreshold = threshold }));
    [Theory]
    [InlineData(0)]
    [InlineData(366)]
    public void RejectsInvalidExpiryWindow(int days) =>
        Assert.Throws<ArgumentException>(() => SettingsManagement.Validate(Valid() with { ExpiryAlertDays = days }));
    [Fact]
    public void ValidatesGstinEmailPrecisionAndInvoicePrefix()
    {
        Assert.Throws<ArgumentException>(() => SettingsManagement.Validate(Valid() with { GstEnabled = true }));
        Assert.Throws<ArgumentException>(() => SettingsManagement.Validate(Valid() with { Gstin = "invalid" }));
        Assert.Throws<ArgumentException>(() => SettingsManagement.Validate(Valid() with { Email = "invalid" }));
        Assert.Throws<ArgumentException>(() => SettingsManagement.Validate(Valid() with { DefaultGstPercentage = 5.001m }));
        Assert.Throws<ArgumentException>(() => SettingsManagement.Validate(Valid() with { InvoicePrefix = "bad/prefix" }));
        Assert.Throws<ArgumentException>(() => SettingsManagement.Validate(Valid() with { ReceiptFooter = new string('a', 501) }));
        SettingsManagement.Validate(Valid() with { GstEnabled = true, Gstin = "27ABCDE1234F1Z5", DefaultGstPercentage = 5m });
    }
}

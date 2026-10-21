using Pharmacy.Domain.Sales;
using Xunit;

namespace Pharmacy.Tests.Taxation;

public sealed class GstCalculatorTests
{
    [Fact]
    public void InclusivePrice_WithDiscount_ExtractsTaxCorrectly()
    {
        var result = GstCalculator.CalculateInclusive(2, 105m, 5m, 10m);
        Assert.Equal(210m, result.GrossAmount);
        Assert.Equal(21m, result.DiscountAmount);
        Assert.Equal(180m, result.TaxableAmount);
        Assert.Equal(9m, result.GstAmount);
        Assert.Equal(189m, result.NetAmount);
    }

    [Fact]
    public void ZeroDiscount_LeavesGrossUnchanged()
    {
        var result = GstCalculator.CalculateInclusive(1, 118m, 18m, 0m);
        Assert.Equal(118m, result.GrossAmount);
        Assert.Equal(0m, result.DiscountAmount);
        Assert.Equal(100m, result.TaxableAmount);
        Assert.Equal(18m, result.GstAmount);
        Assert.Equal(118m, result.NetAmount);
    }

    [Fact]
    public void FullDiscount_ProducesZeroTaxAndNet()
    {
        var result = GstCalculator.CalculateInclusive(3, 100m, 12m, 100m);
        Assert.Equal(300m, result.GrossAmount);
        Assert.Equal(300m, result.DiscountAmount);
        Assert.Equal(0m, result.TaxableAmount);
        Assert.Equal(0m, result.GstAmount);
        Assert.Equal(0m, result.NetAmount);
    }

    [Fact]
    public void FractionalAmounts_UseAwayFromZeroRounding()
    {
        var result = GstCalculator.CalculateInclusive(1, 0.05m, 5m, 10m);
        Assert.Equal(0.05m, result.GrossAmount);
        Assert.Equal(0.01m, result.DiscountAmount);
        Assert.Equal(0.04m, result.NetAmount);
        Assert.Equal(0.04m, result.TaxableAmount);
        Assert.Equal(0m, result.GstAmount);
    }

    [Theory]
    [InlineData(0, 100, 5, 0)]
    [InlineData(-1, 100, 5, 0)]
    [InlineData(1, -1, 5, 0)]
    [InlineData(1, 100, -1, 0)]
    [InlineData(1, 100, 101, 0)]
    [InlineData(1, 100, 5, -1)]
    [InlineData(1, 100, 5, 101)]
    public void InvalidInputs_ThrowArgumentException(int quantity, int unitPrice, int gst, int discount)
    {
        Assert.Throws<ArgumentException>(() =>
            GstCalculator.CalculateInclusive(quantity, unitPrice, gst, discount));
    }

    [Fact]
    public void Result_ReconcilesExactly()
    {
        var result = GstCalculator.CalculateInclusive(7, 99.99m, 12m, 7.5m);
        Assert.Equal(result.GrossAmount - result.DiscountAmount, result.NetAmount);
        Assert.Equal(result.TaxableAmount + result.GstAmount, result.NetAmount);
    }
}

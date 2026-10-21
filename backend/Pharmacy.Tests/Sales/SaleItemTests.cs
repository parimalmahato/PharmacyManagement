using Pharmacy.Domain.Entities.Sales;
using Xunit;

namespace Pharmacy.Tests.Sales;

public sealed class SaleItemTests
{
    [Fact]
    public void Constructor_SnapshotsMedicineTaxAndCalculatesAmounts()
    {
        var item = new SaleItem(1, 10, 2, 105m, "Paracetamol", "3004", 5m, 10m);
        Assert.Equal(1, item.MedicineId);
        Assert.Equal(10, item.MedicineBatchId);
        Assert.Equal("Paracetamol", item.MedicineNameSnapshot);
        Assert.Equal("3004", item.HsnCode);
        Assert.Equal(5m, item.GstPercentage);
        Assert.Equal(10m, item.DiscountPercentage);
        Assert.Equal(210m, item.GrossAmount);
        Assert.Equal(21m, item.DiscountAmount);
        Assert.Equal(180m, item.TaxableAmount);
        Assert.Equal(9m, item.GstAmount);
        Assert.Equal(189m, item.LineTotal);
    }

    [Fact]
    public void Constructor_TrimsSnapshotStrings()
    {
        var item = new SaleItem(1, 2, 1, 100m, "  Test  ", "  3004  ", 0m);
        Assert.Equal("Test", item.MedicineNameSnapshot);
        Assert.Equal("3004", item.HsnCode);
        Assert.Equal(100m, item.LineTotal);
    }

    [Theory]
    [InlineData(0, 1, 1, "Valid")]
    [InlineData(1, 0, 1, "Valid")]
    [InlineData(1, 1, 0, "Valid")]
    [InlineData(1, 1, 1, " ")]
    public void Constructor_RejectsInvalidIdentifiersQuantityOrName(int medicineId, int batchId, int quantity, string name)
    {
        Assert.Throws<ArgumentException>(() =>
            new SaleItem(medicineId, batchId, quantity, 100m, name, null, 5m));
    }
}

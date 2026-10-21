using Pharmacy.Domain.Entities.Sales;
using Xunit;

namespace Pharmacy.Tests.Sales;

public sealed class SaleCalculationTests
{
    [Fact]
    public void AddingItems_AggregatesGrossDiscountTaxAndPayable()
    {
        var sale = new Sale("SAL-TEST-001");
        sale.AddItem(new SaleItem(1, 11, 2, 105m, "A", "3004", 5m, 10m));
        sale.AddItem(new SaleItem(2, 22, 1, 118m, "B", "3004", 18m));

        Assert.Equal(328m, sale.SubtotalAmount);
        Assert.Equal(21m, sale.DiscountAmount);
        Assert.Equal(280m, sale.TaxableAmount);
        Assert.Equal(27m, sale.GstAmount);
        Assert.Equal(307m, sale.TotalAmount);
        Assert.Equal(2, sale.Items.Count);
        Assert.Equal(sale.SubtotalAmount - sale.DiscountAmount, sale.TotalAmount);
        Assert.Equal(sale.TaxableAmount + sale.GstAmount, sale.TotalAmount);
    }

    [Fact]
    public void SplitAcrossBatches_UsesEachBatchPrice()
    {
        var sale = new Sale("SAL-TEST-002");
        sale.AddItem(new SaleItem(1, 11, 2, 105m, "A", "3004", 5m));
        sale.AddItem(new SaleItem(1, 12, 1, 210m, "A", "3004", 5m));

        Assert.Equal(420m, sale.SubtotalAmount);
        Assert.Equal(400m, sale.TaxableAmount);
        Assert.Equal(20m, sale.GstAmount);
        Assert.Equal(420m, sale.TotalAmount);
        Assert.Equal(2, sale.Items.Count);
    }

    [Fact]
    public void Sale_RejectsEmptyInvoiceNumber()
    {
        Assert.Throws<ArgumentException>(() => new Sale(" "));
    }

    [Fact]
    public void Sale_RejectsNullItem()
    {
        var sale = new Sale("SAL-TEST-003");
        Assert.Throws<ArgumentNullException>(() => sale.AddItem(null!));
    }

    [Fact]
    public void Customer_CanBeAssignedBeforeSaving()
    {
        var sale = new Sale("SAL-TEST-004");
        sale.AssignCustomer(7);
        Assert.Equal(7, sale.CustomerId);
    }

    [Fact]
    public void Customer_RejectsNonPositiveId()
    {
        var sale = new Sale("SAL-TEST-005");
        Assert.Throws<ArgumentException>(() => sale.AssignCustomer(0));
    }
}

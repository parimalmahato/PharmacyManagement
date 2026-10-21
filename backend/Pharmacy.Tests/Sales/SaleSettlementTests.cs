using Pharmacy.Domain.Entities.Sales.Returns;

namespace Pharmacy.Tests.Sales;

public sealed class SaleSettlementTests
{
    [Fact]
    public void CheckoutRequiresANonemptyKeyAndFingerprint()
    {
        var sale = new Pharmacy.Domain.Entities.Sales.Sale("TEST");
        Assert.Throws<ArgumentException>(() => sale.IdentifyCheckout(Guid.Empty, new string('A', 64)));
        Assert.Throws<ArgumentException>(() => sale.IdentifyCheckout(Guid.NewGuid(), "short"));
        var key = Guid.NewGuid(); sale.IdentifyCheckout(key, new string('A', 64));
        Assert.Equal(key, sale.CheckoutKey);
    }
    [Theory]
    [InlineData(ReturnDisposition.Damaged)]
    [InlineData(ReturnDisposition.Expired)]
    [InlineData(ReturnDisposition.Quarantine)]
    [InlineData(ReturnDisposition.NonResaleable)]
    [InlineData((ReturnDisposition)0)]
    [InlineData((ReturnDisposition)99)]
    public void UnsafeOrUnknownDispositionsCannotBeRestocked(ReturnDisposition disposition) =>
        Assert.Throws<ArgumentException>(() => new SaleReturnItem(1, 1, true, 10, disposition));

    [Fact]
    public void UninspectedReturnsDefaultToQuarantine()
    {
        Assert.Equal(ReturnDisposition.Quarantine, new SaleReturnItem(1, 1, false, 10).Disposition);
        Assert.Throws<ArgumentException>(() => new SaleReturnItem(1, 1, true, 10));
        Assert.True(new SaleReturnItem(1, 1, true, 10, ReturnDisposition.Saleable, true, "inspector").Restock);
    }

    [Fact]
    public void SaleableStockNeedsExplicitInspectionAndTrustedIdentity()
    {
        Assert.Throws<ArgumentException>(() => new SaleReturnItem(1, 1, true, 10, ReturnDisposition.Saleable));
        Assert.Throws<ArgumentException>(() => new SaleReturnItem(1, 1, true, 10, ReturnDisposition.Saleable, true));
        Assert.Throws<ArgumentException>(() => new SaleReturnItem(1, 1, true, 10, ReturnDisposition.Saleable, true, " "));
        var item = new SaleReturnItem(1, 1, true, 10, ReturnDisposition.Saleable, true, "inspector");
        Assert.Equal("inspector", item.InspectedByUserId);
        Assert.NotNull(item.InspectedAtUtc);
    }

    [Fact]
    public void RefundMethodsAndReferencesAreValidated()
    {
        Assert.Throws<ArgumentException>(() => new SaleRefund(1, (RefundMethod)99, 10, null, Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => new SaleRefund(1, RefundMethod.Card, 10, null, Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => new SaleRefund(1, RefundMethod.Cash, -1, null, Guid.NewGuid()));
    }
    [Theory]
    [InlineData(0, 40, 0, 60, 0)]
    [InlineData(50, 40, 0, 10, 0)]
    [InlineData(100, 40, 0, 0, 40)]
    [InlineData(100, 40, 25, 0, 15)]
    [InlineData(100, 100, 100, 0, 0)]
    public void ReturnsOffsetBalanceBeforeRefunds(decimal paid, decimal returned, decimal refunded, decimal balance, decimal due)
    {
        var settlement = new SaleSettlement(100, paid, returned, refunded);
        Assert.Equal(balance, settlement.BalanceAmount);
        Assert.Equal(due, settlement.RefundDue);
        Assert.Equal(100m - returned, settlement.NetTotal);
    }

    [Fact]
    public void PartialReturnsPreserveOriginalDiscountedTotal()
    {
        Assert.Equal(3.33m, SaleSettlement.ReturnCredit(10m, 3, 0, 1));
        Assert.Equal(3.34m, SaleSettlement.ReturnCredit(10m, 3, 1, 1));
        Assert.Equal(3.33m, SaleSettlement.ReturnCredit(10m, 3, 2, 1));
        Assert.Equal(0m, SaleSettlement.ReturnCredit(0m, 3, 0, 3));
    }

    [Fact]
    public void CannotReturnMoreThanSoldOrNonPositiveQuantity()
    {
        Assert.Throws<ArgumentException>(() => SaleSettlement.ReturnCredit(10, 3, 2, 2));
        Assert.Throws<ArgumentException>(() => SaleSettlement.ReturnCredit(10, 3, 0, 0));
        Assert.Throws<ArgumentException>(() => new SaleReturn(1, " ", Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => new SaleReturnItem(1, -1, true, 5m));
    }
}

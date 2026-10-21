using Pharmacy.Domain.Entities.Purchasing;
using Xunit;

namespace Pharmacy.Tests.Purchasing;

public sealed class PurchaseInvoiceTests
{
    [Fact]
    public void AddItem_ShouldCalculateTotalAmount()
    {
        var purchase = new PurchaseInvoice(
            supplierId: 1,
            invoiceNumber: "INV-1001",
            invoiceDate: DateOnly.FromDateTime(DateTime.UtcNow));

        var item = new PurchaseItem(
            medicineId: 1,
            batchNumber: "PCM-001",
            expiryDate: DateOnly.FromDateTime(
                DateTime.UtcNow.AddYears(1)),
            quantity: 100,
            purchasePrice: 2.50m,
            sellingPrice: 3.50m);

        purchase.AddItem(item);

        Assert.Equal(250m, purchase.TotalAmount);
        Assert.Equal(PurchaseStatus.Draft, purchase.Status);
    }

    [Fact]
    public void MarkReceived_ShouldChangeStatusToReceived()
    {
        var purchase = CreatePurchaseWithItem();

        purchase.MarkReceived();

        Assert.Equal(PurchaseStatus.Received, purchase.Status);
    }

    [Fact]
    public void MarkReceived_ShouldRejectEmptyPurchase()
    {
        var purchase = new PurchaseInvoice(
            1,
            "INV-1002",
            DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.Throws<InvalidOperationException>(
            () => purchase.MarkReceived());
    }

    [Fact]
    public void MarkReceived_ShouldRejectSecondReceipt()
    {
        var purchase = CreatePurchaseWithItem();

        purchase.MarkReceived();

        Assert.Throws<InvalidOperationException>(
            () => purchase.MarkReceived());
    }

    private static PurchaseInvoice CreatePurchaseWithItem()
    {
        var purchase = new PurchaseInvoice(
            1,
            "INV-1003",
            DateOnly.FromDateTime(DateTime.UtcNow));

        purchase.AddItem(new PurchaseItem(
            1,
            "PCM-002",
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),
            10,
            5m,
            7m));

        return purchase;
    }
}
using Pharmacy.Domain.Entities.Payments;

namespace Pharmacy.Tests.Payments;

public sealed class PaymentTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1.001)]
    [InlineData(10000000000000000)]
    public void RejectsInvalidAmounts(decimal amount) =>
        Assert.Throws<ArgumentException>(() => new Payment(1, PaymentMethod.Cash, amount, null, Guid.NewGuid()));

    [Theory]
    [InlineData(PaymentMethod.Upi)]
    [InlineData(PaymentMethod.Card)]
    public void ElectronicPaymentsRequireReference(PaymentMethod method) =>
        Assert.Throws<ArgumentException>(() => new Payment(1, method, 10m, " ", Guid.NewGuid()));

    [Fact]
    public void NormalizesReference()
    {
        var payment = new Payment(1, PaymentMethod.Upi, 10.25m, "  REF123  ", Guid.NewGuid());
        Assert.Equal("REF123", payment.Reference);
        Assert.Equal(10.25m, payment.Amount);
    }

    [Fact]
    public void RequiresValidSaleMethodAndKey()
    {
        Assert.Throws<ArgumentException>(() => new Payment(0, PaymentMethod.Cash, 1m, null, Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => new Payment(1, (PaymentMethod)99, 1m, null, Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => new Payment(1, PaymentMethod.Cash, 1m, null, Guid.Empty));
        Assert.Throws<ArgumentException>(() => new Payment(1, PaymentMethod.Card, 1m, new string('x', 151), Guid.NewGuid()));
    }
}


namespace Pharmacy.Domain.Sales;

/// <summary>
/// The rounded gross, discount, taxable, GST and final values calculated from an inclusive selling price.
/// </summary>
public sealed record GstCalculation(
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal TaxableAmount,
    decimal GstAmount,
    decimal NetAmount);

/// <summary>
/// Centralizes GST-inclusive sale-line arithmetic so invoice totals use consistent monetary rounding.
/// </summary>
public static class GstCalculator
{
    /// <summary>
    /// Calculates gross, discount, taxable value and GST from an inclusive unit price using consistent two-decimal monetary rounding.
    /// </summary>
    public static GstCalculation CalculateInclusive(
        int quantity,
        decimal unitPrice,
        decimal gstPercentage,
        decimal discountPercentage)
    {
        if (quantity <= 0)
            throw new ArgumentException(
                "Quantity must be positive.");

        if (unitPrice < 0)
            throw new ArgumentException(
                "Unit price cannot be negative.");

        if (gstPercentage < 0 || gstPercentage > 100)
            throw new ArgumentException(
                "GST percentage must be between 0 and 100.");

        if (discountPercentage < 0 ||
            discountPercentage > 100)
            throw new ArgumentException(
                "Discount percentage must be between 0 and 100.");

        static decimal Round(decimal amount) =>
            decimal.Round(
                amount,
                2,
                MidpointRounding.AwayFromZero);

        decimal grossAmount = Round(quantity * unitPrice);

        decimal discountAmount = Round(
            grossAmount * discountPercentage / 100m);

        decimal netAmount = grossAmount - discountAmount;

        decimal taxableAmount = Round(
            netAmount * 100m / (100m + gstPercentage));

        decimal gstAmount = netAmount - taxableAmount;

        return new GstCalculation(
            grossAmount,
            discountAmount,
            taxableAmount,
            gstAmount,
            netAmount);
    }
}

namespace Pharmacy.Domain.Entities.Sales.Returns;

/// <summary>
/// Derives net payable, outstanding balance and refund liability from original invoice, return credits, receipts and payouts.
/// </summary>
public sealed record SaleSettlement(decimal OriginalTotal, decimal PaidAmount, decimal ReturnedAmount, decimal RefundedAmount)
{
    /// <summary>
    /// Original invoice total after recorded credits; original invoice snapshots remain unchanged.
    /// </summary>
    public decimal NetTotal => OriginalTotal - ReturnedAmount;
    /// <summary>
    /// Recorded receipts less actual recorded refund payouts.
    /// </summary>
    public decimal NetPaid => PaidAmount - RefundedAmount;
    /// <summary>
    /// Non-negative customer receivable after comparing adjusted invoice value with net receipts.
    /// </summary>
    public decimal BalanceAmount => Math.Max(0, NetTotal - NetPaid);
    /// <summary>
    /// Non-negative liability to the customer when net receipts exceed adjusted invoice value; it is not an initiated payout.
    /// </summary>
    public decimal RefundDue => Math.Max(0, NetPaid - NetTotal);
    /// <summary>
    /// Payment coverage derived from net receipts and adjusted invoice value.
    /// </summary>
    public string PaymentStatus => NetPaid >= NetTotal ? "Paid" : NetPaid > 0 ? "PartiallyPaid" : "Unpaid";

    // Cumulative rounding ensures repeated partial returns credit exactly the
    // original discounted line total when the final unit is returned.
    /// <summary>
    /// Computes the next partial-return credit using cumulative rounding so the final returned unit completes exactly the original discounted line total.
    /// </summary>
    /// <param name="lineTotal">Original discounted GST-inclusive line amount in INR.</param>
    /// <param name="soldQuantity">Original inventory-unit quantity on the issued sale line.</param>
    /// <param name="previouslyReturned">Units already credited against that line.</param>
    /// <param name="quantity">Positive additional units being returned now.</param>
    /// <returns>The incremental credit rounded to two decimal places.</returns>
    /// <exception cref="ArgumentException">Amounts or quantities are invalid, or the return exceeds the remaining original quantity.</exception>
    public static decimal ReturnCredit(decimal lineTotal, int soldQuantity, int previouslyReturned, int quantity)
    {
        if (lineTotal < 0 || soldQuantity <= 0 || previouslyReturned < 0 || quantity <= 0 ||
            previouslyReturned > soldQuantity || quantity > soldQuantity - previouslyReturned)
            throw new ArgumentException("Return quantity exceeds the remaining sold quantity.");
        return decimal.Round(lineTotal * ((decimal)previouslyReturned + quantity) / soldQuantity, 2, MidpointRounding.AwayFromZero)
            - decimal.Round(lineTotal * previouslyReturned / soldQuantity, 2, MidpointRounding.AwayFromZero);
    }
}

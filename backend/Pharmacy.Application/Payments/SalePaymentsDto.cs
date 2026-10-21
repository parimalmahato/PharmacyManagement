namespace Pharmacy.Application.Payments.DTOs;

/// <summary>
/// Original invoice and current receipt/refund settlement information displayed by the payment screen.
/// </summary>
public sealed record SalePaymentsDto(
    int SaleId,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal BalanceAmount,
    string PaymentStatus,
    IReadOnlyList<PaymentDto> Payments)
{
    public decimal ReturnedAmount { get; init; }
    public decimal RefundedAmount { get; init; }
    public decimal RefundDue { get; init; }
    public bool IsCancelled { get; init; }
    public decimal NetTotal => IsCancelled ? 0m : TotalAmount - ReturnedAmount;
}

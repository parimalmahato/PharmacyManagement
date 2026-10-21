using Pharmacy.Application.Payments.DTOs;

namespace Pharmacy.Application.Payments;

/// <summary>
/// Defines confirmed receipt recording and invoice settlement reads; it never initiates external payment processing.
/// </summary>
public interface IPaymentRepository
{
    /// <summary>
    /// Validates a confirmed receipt against the current balance and records it with idempotency and audit protection in a transaction.
    /// </summary>
    Task<PaymentDto> RecordAsync(int saleId, RecordPaymentRequestDto request, CancellationToken cancellationToken = default);
    /// <summary>
    /// Returns receipt history and current settlement for a branch-visible invoice or null when absent.
    /// </summary>
    Task<SalePaymentsDto?> GetBySaleAsync(int saleId, CancellationToken cancellationToken = default);
}

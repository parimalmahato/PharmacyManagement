using Pharmacy.Domain.Entities.Payments;

namespace Pharmacy.Application.Payments.DTOs;

/// <summary>
/// A confirmed receipt&apos;s method, amount, reference and retry key; it is not a bank processing instruction.
/// </summary>
public sealed record RecordPaymentRequestDto(
    PaymentMethod Method,
    decimal Amount,
    string? Reference,
    Guid IdempotencyKey);
namespace Pharmacy.Application.Payments.DTOs;

/// <summary>
/// A posted receipt ledger entry with reference, retry identity and UTC creation time.
/// </summary>
public sealed record PaymentDto(
    long Id,
    int SaleId,
    string Method,
    decimal Amount,
    string? Reference,
    Guid IdempotencyKey,
    DateTime CreatedAtUtc);
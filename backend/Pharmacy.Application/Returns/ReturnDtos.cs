using Pharmacy.Application.Payments.DTOs;
using Pharmacy.Domain.Entities.Sales.Returns;

namespace Pharmacy.Application.Returns;

/// <summary>
/// Original sold-line quantity, disposition and explicit inspection/restocking choices for a return.
/// </summary>
public sealed record ReturnItemRequest(int SaleItemId, int Quantity, bool Restock = false, ReturnDisposition Disposition = ReturnDisposition.Quarantine, bool InspectionApproved = false);
/// <summary>
/// Confirmed payout details optionally saved atomically with a return; no external transfer is initiated.
/// </summary>
public sealed record RefundDetails(RefundMethod Method, decimal Amount, string? Reference);
/// <summary>
/// Confirmed refund method, amount, reference and retry identity for settlement validation.
/// </summary>
public sealed record RefundRequest(RefundMethod Method, decimal Amount, string? Reference, Guid IdempotencyKey);
/// <summary>
/// A refund ledger entry linked to a sale and optionally a return event.
/// </summary>
public sealed record RefundDto(long Id, int SaleId, long? SaleReturnId, string Method, decimal Amount, string? Reference, Guid IdempotencyKey, DateTime CreatedAtUtc);
/// <summary>
/// A reason, original sale-line returns and optional confirmed payout saved through one return operation.
/// </summary>
public sealed record CreateReturnRequest(string Reason, Guid IdempotencyKey, List<ReturnItemRequest> Items, RefundDetails? Refund = null);
/// <summary>
/// Recorded returned quantity and credit with disposition, inspection and restocking details.
/// </summary>
public sealed record ReturnItemDto(int SaleItemId, int Quantity, bool Restock, decimal Amount, string Disposition,
    bool InspectionApproved, string? InspectedByUserId, DateTime? InspectedAtUtc);
/// <summary>
/// A preserved return event with its reason, source-line credits and associated refund information.
/// </summary>
public sealed record ReturnDto(long Id, int SaleId, string Reason, Guid IdempotencyKey, DateTime CreatedAtUtc,
    decimal TotalAmount, IReadOnlyList<ReturnItemDto> Items)
{
    public RefundDto? Refund { get; init; }
}
/// <summary>
/// Original sold batch details and remaining eligible return quantity used by the return review screen.
/// </summary>
public sealed record ReturnableItemDto(int SaleItemId, string MedicineName, string BatchNumber, DateOnly ExpiryDate,
    int SoldQuantity, int ReturnedQuantity, int RemainingQuantity, decimal LineTotal, bool CanRestock);
/// <summary>
/// The original sale&apos;s returnable lines, recorded returns, payout history and current settlement.
/// </summary>
public sealed record SaleReturnsDto(int SaleId, IReadOnlyList<ReturnableItemDto> Items, IReadOnlyList<ReturnDto> Returns,
    IReadOnlyList<RefundDto> Refunds, decimal ReturnedAmount, decimal RefundedAmount, decimal RefundDue);

/// <summary>
/// Defines return credits, inspection-controlled restocking and confirmed refund recording against original sales.
/// </summary>
public interface ISaleReturnRepository
{
    /// <summary>
    /// Returns original returnable lines, recorded return/payout history and current settlement for a branch-visible sale.
    /// </summary>
    Task<SaleReturnsDto?> GetAsync(int saleId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Validates remaining original-line quantities and disposition, then commits return credits, approved opt-in restocking and an optional confirmed payout together.
    /// </summary>
    Task<ReturnDto> CreateAsync(int saleId, CreateReturnRequest request, CancellationToken cancellationToken = default);
    /// <summary>
    /// Records an actual payout only within the remaining refund liability, using retry protection and audit history rather than initiating a transfer.
    /// </summary>
    Task<RefundDto> RefundAsync(int saleId, RefundRequest request, CancellationToken cancellationToken = default);
}

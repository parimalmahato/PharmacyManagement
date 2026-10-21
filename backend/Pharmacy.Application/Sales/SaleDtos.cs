
namespace Pharmacy.Application.Sales;

/// <summary>
/// Requested medicine quantity and discount; eligible batch allocation and final totals are determined by checkout.
/// </summary>
public sealed record CreateSaleItemRequest(
    int MedicineId,
    int Quantity,
    decimal DiscountPercentage = 0m);

/// <summary>
/// Reason, retry identity and custody/restocking confirmations required to preserve an invoice as cancelled.
/// </summary>
public sealed record CancelSaleRequest(string Reason, Guid IdempotencyKey,
    bool MedicinesNotDispensed, bool Restock = false, bool InspectionApproved = false);

/// <summary>
/// Checkout input with optional customer and retry identity, validated before any invoice or stock changes commit.
/// </summary>
public sealed record CreateSaleRequest(
    List<CreateSaleItemRequest> Items,
    int? CustomerId = null,
    Guid? IdempotencyKey = null
    );



/// <summary>
/// Original invoice-line values including batch allocation and GST-inclusive financial snapshots.
/// </summary>
public sealed record SaleItemDto(
    int MedicineId,
    string MedicineName,
    string BatchNumber,
    DateOnly ExpiryDate,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string? HsnCode,
    decimal GstPercentage,
    decimal DiscountPercentage,
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal TaxableAmount,
    decimal GstAmount);

/// <summary>
/// Sale history data with original totals and derived current settlement and cancellation information.
/// </summary>
public sealed record SaleDto(
    int Id,
    string InvoiceNumber,
    DateTime CreatedAtUtc,
    decimal TotalAmount,
    string Status,
    List<SaleItemDto> Items,
    int? CustomerId,
    string? CustomerName,
    string? CustomerPhone,
    decimal SubtotalAmount,
    decimal DiscountAmount,
    decimal TaxableAmount,
    decimal GstAmount)
{
    public decimal PaidAmount { get; init; }
    public decimal ReturnedAmount { get; init; }
    public decimal RefundedAmount { get; init; }
    public DateTime? CancelledAtUtc { get; init; }
    public string? CancelledByUserId { get; init; }
    public string? CancellationReason { get; init; }
    public bool CancellationStockRestored { get; init; }
    /// <summary>
    /// Adjusted invoice amount, with a cancelled invoice treated as zero payable without changing its original total.
    /// </summary>
    public decimal NetTotal => Status == "Cancelled" ? 0m : TotalAmount - ReturnedAmount;
    /// <summary>
    /// Remaining non-negative amount owed by the customer after credits, receipts and refunds.
    /// </summary>
    public decimal BalanceAmount => Math.Max(0, NetTotal - (PaidAmount - RefundedAmount));
    /// <summary>
    /// Remaining non-negative amount owed to the customer; staff must make and record the actual payout separately.
    /// </summary>
    public decimal RefundDue => Math.Max(0, PaidAmount - RefundedAmount - NetTotal);
    public string PaymentStatus => PaidAmount - RefundedAmount >= NetTotal ? "Paid" : PaidAmount - RefundedAmount > 0 ? "PartiallyPaid" : "Unpaid";
}


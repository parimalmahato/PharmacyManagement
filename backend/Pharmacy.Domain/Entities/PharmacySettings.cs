
namespace Pharmacy.Domain.Entities;

/// <summary>
/// Branch configuration for pharmacy identity, invoice sequencing, alert thresholds, receipt preferences and optional direct UPI collection.
/// </summary>
public sealed class PharmacySettings
{
    public int Id { get; set; }
    public int SingletonKey { get; set; } = 1;
    /// <summary>
    /// Optimistic concurrency token used to prevent silent overwriting of another settings update.
    /// </summary>
    public Guid Revision { get; set; } = Guid.NewGuid();
    /// <summary>
    /// Branch-managed invoice sequence state; ordinary settings edits must preserve already-issued numbering.
    /// </summary>
    public long NextInvoiceNumber { get; set; } = 1;
    public int InvoiceNumberDigits { get; set; } = 6;
    /// <summary>
    /// Batch-level quantity threshold used by configured low-stock reporting and inventory query workflows.
    /// </summary>
    public int LowStockThreshold { get; set; } = 10;
    public bool ExpiryAlertsEnabled { get; set; } = true;
    public int ExpiryAlertDays { get; set; } = 90;
    public string ReceiptFooter { get; set; } = "Thank you for your purchase!";
    public bool ReceiptShowCustomerPhone { get; set; } = true;
    public bool ReceiptShowPaymentHistory { get; set; } = true;
    public bool UpiEnabled { get; set; }
    public string? UpiId { get; set; }
    public string? UpiPayeeName { get; set; }
    public string? UpiMerchantCode { get; set; }

    public string PharmacyName { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Gstin { get; set; }

    public string InvoicePrefix { get; set; } = "SAL";

    public bool GstEnabled { get; set; }

    public decimal DefaultGstPercentage { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

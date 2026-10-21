
namespace Pharmacy.Application.Settings;

/// <summary>
/// Current branch profile and receipt/alert/UPI preferences without exposing editable invoice sequence ownership.
/// </summary>
public sealed record PharmacySettingsDto(
    int Id,
    string PharmacyName,
    string Address,
    string PhoneNumber,
    string? Email,
    string? Gstin,
    string InvoicePrefix,
    bool GstEnabled,
    decimal DefaultGstPercentage,
    DateTime UpdatedAtUtc
)
{
    public Guid Revision { get; init; }
    public long NextInvoiceNumber { get; init; }
    public int InvoiceNumberDigits { get; init; }
    public int LowStockThreshold { get; init; }
    public bool ExpiryAlertsEnabled { get; init; }
    public int ExpiryAlertDays { get; init; }
    public string ReceiptFooter { get; init; } = "";
    public bool ReceiptShowCustomerPhone { get; init; }
    public bool ReceiptShowPaymentHistory { get; init; }
    public bool UpiEnabled { get; init; }
    public string? UpiId { get; init; }
    public string? UpiPayeeName { get; init; }
    public string? UpiMerchantCode { get; init; }
}

/// <summary>
/// Branch settings input carrying the expected revision for concurrent-update protection.
/// </summary>
public sealed record UpdatePharmacySettingsRequest(
    string PharmacyName,
    string Address,
    string PhoneNumber,
    string? Email,
    string? Gstin,
    string InvoicePrefix,
    bool GstEnabled,
    decimal DefaultGstPercentage,
    int InvoiceNumberDigits = 6,
    int LowStockThreshold = 10,
    bool ExpiryAlertsEnabled = true,
    int ExpiryAlertDays = 90,
    string ReceiptFooter = "Thank you for your purchase!",
    bool ReceiptShowCustomerPhone = true,
    bool ReceiptShowPaymentHistory = true,
    Guid? Revision = null,
    bool UpiEnabled = false,
    string? UpiId = null,
    string? UpiPayeeName = null,
    string? UpiMerchantCode = null
);

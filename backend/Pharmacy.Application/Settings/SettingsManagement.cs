using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Settings;

/// <summary>
/// Defines reading and updating the current branch profile while preserving the managed invoice sequence.
/// </summary>
public interface IPharmacySettingsRepository
{
    /// <summary>
    /// Returns the active branch profile when configured, without exposing other branches&apos; settings.
    /// </summary>
    Task<PharmacySettingsDto?> GetAsync(CancellationToken ct = default);
    /// <summary>
    /// Validates and revision-checks branch profile changes while preserving the managed invoice sequence and recording an audit event.
    /// </summary>
    Task<PharmacySettingsDto> UpdateAsync(UpdatePharmacySettingsRequest request, CancellationToken ct = default);
}
/// <summary>
/// Validates branch profile, invoice, alert and UPI preferences before configuration is saved.
/// </summary>
public static class SettingsManagement
{
    /// <summary>
    /// Checks pharmacy identity, invoice preferences, threshold ranges and optional UPI collection configuration before persistence.
    /// </summary>
    public static void Validate(UpdatePharmacySettingsRequest r)
    {
        static bool Required(string? s, int length) => !string.IsNullOrWhiteSpace(s) && s.Trim().Length <= length;
        if (!Required(r.PharmacyName, 200) || !Required(r.Address, 500) || !Required(r.PhoneNumber, 20))
            throw new ArgumentException("Pharmacy name, address and phone are required and must fit their length limits.");
        if (r.Email?.Length > 150 || (!string.IsNullOrWhiteSpace(r.Email) && !new EmailAddressAttribute().IsValid(r.Email.Trim())))
            throw new ArgumentException("Enter a valid email of at most 150 characters.");
        if (!Required(r.InvoicePrefix, 20) || !Regex.IsMatch(r.InvoicePrefix.Trim(), "^[A-Za-z0-9-]+$"))
            throw new ArgumentException("Invoice prefix must contain 1–20 letters, digits or hyphens.");
        if (!string.IsNullOrWhiteSpace(r.Gstin) && !Regex.IsMatch(r.Gstin.Trim().ToUpperInvariant(), "^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$"))
            throw new ArgumentException("GSTIN must have the 15-character GSTIN format.");
        if (r.GstEnabled && string.IsNullOrWhiteSpace(r.Gstin)) throw new ArgumentException("GSTIN is required when GST is enabled.");
        if (r.DefaultGstPercentage < 0 || r.DefaultGstPercentage > 100 || decimal.Round(r.DefaultGstPercentage, 2) != r.DefaultGstPercentage)
            throw new ArgumentException("Default GST must be 0–100 with at most two decimal places.");
        if (r.InvoiceNumberDigits is < 3 or > 12 || r.LowStockThreshold is < 1 or > 100000 || r.ExpiryAlertDays is < 1 or > 365)
            throw new ArgumentException("Invoice digits must be 3–12, low stock threshold 1–100000, and expiry alert window 1–365 days.");
        if (r.ReceiptFooter is null || r.ReceiptFooter.Length > 500) throw new ArgumentException("Receipt footer must be at most 500 characters.");
        Pharmacy.Application.Payments.UpiPaymentRequests.ValidateConfiguration(r.UpiEnabled, r.UpiId, r.UpiPayeeName, r.UpiMerchantCode);
    }
    /// <summary>
    /// Maps stored branch settings and their revision into the application view, including read-only invoice sequence information.
    /// </summary>
    public static PharmacySettingsDto ToDto(PharmacySettings s) => new(s.Id, s.PharmacyName, s.Address, s.PhoneNumber,
        s.Email, s.Gstin, s.InvoicePrefix, s.GstEnabled, s.DefaultGstPercentage, s.UpdatedAtUtc)
    {
        UpiEnabled = s.UpiEnabled, UpiId = s.UpiId, UpiPayeeName = s.UpiPayeeName, UpiMerchantCode = s.UpiMerchantCode,
        Revision = s.Revision, NextInvoiceNumber = s.NextInvoiceNumber, InvoiceNumberDigits = s.InvoiceNumberDigits,
        LowStockThreshold = s.LowStockThreshold, ExpiryAlertsEnabled = s.ExpiryAlertsEnabled, ExpiryAlertDays = s.ExpiryAlertDays,
        ReceiptFooter = s.ReceiptFooter, ReceiptShowCustomerPhone = s.ReceiptShowCustomerPhone, ReceiptShowPaymentHistory = s.ReceiptShowPaymentHistory
    };
}

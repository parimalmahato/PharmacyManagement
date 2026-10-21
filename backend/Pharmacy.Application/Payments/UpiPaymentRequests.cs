using System.Globalization;
using System.Text.RegularExpressions;
using Pharmacy.Application.Sales;
using Pharmacy.Application.Settings;

namespace Pharmacy.Application.Payments;

/// <summary>
/// An unsigned UPI payment URI and payee details for an outstanding invoice; the reference is not a verified bank UTR.
/// </summary>
public sealed record UpiPaymentRequestDto(int SaleId, string InvoiceNumber, string PayeeUpiId,
    string PayeeName, decimal Amount, decimal OutstandingBalance, string Reference, string PaymentUri, DateTime GeneratedAtUtc);

/// <summary>
/// Creates a direct UPI collection request using the active branch&apos;s payee configuration and current invoice balance.
/// </summary>
public interface IUpiPaymentRequestService
{
    /// <summary>
    /// Loads the current invoice and branch payee profile before creating a bounded unsigned UPI collection request.
    /// </summary>
    Task<UpiPaymentRequestDto> GetAsync(int saleId, decimal? amount, CancellationToken ct = default);
}

/// <summary>
/// Loads the current sale and branch profile before building an unsigned UPI request without posting a payment.
/// </summary>
public sealed class UpiPaymentRequestService(ISaleRepository sales, IPharmacySettingsRepository settings) : IUpiPaymentRequestService
{
    /// <summary>
    /// Loads the current invoice and branch payee profile before creating a bounded unsigned UPI collection request.
    /// </summary>
    public async Task<UpiPaymentRequestDto> GetAsync(int saleId, decimal? amount, CancellationToken ct = default)
    {
        if (saleId <= 0) throw new ArgumentException("Invalid sale ID.");
        var sale = await sales.GetByIdAsync(saleId, ct) ?? throw new KeyNotFoundException("Sale not found.");
        var profile = await settings.GetAsync(ct) ?? throw new InvalidOperationException("Configure this branch's pharmacy settings first.");
        return UpiPaymentRequests.Create(sale, profile, amount);
    }
}

/// <summary>
/// Validates merchant settings and builds UPI payment URIs; generating a QR request is not evidence of settlement.
/// </summary>
public static class UpiPaymentRequests
{
    /// <summary>
    /// Checks required enabled-payee fields, UPI ID format and optional bank-supplied merchant code without verifying bank registration.
    /// </summary>
    public static void ValidateConfiguration(bool enabled, string? upiId, string? payeeName, string? merchantCode)
    {
        if (enabled && (string.IsNullOrWhiteSpace(upiId) || string.IsNullOrWhiteSpace(payeeName)))
            throw new ArgumentException("UPI ID and payee name are required when UPI collection is enabled.");
        if (!string.IsNullOrWhiteSpace(upiId) && (upiId.Trim().Length > 100 || !Regex.IsMatch(upiId.Trim(), @"^[A-Za-z0-9][A-Za-z0-9._-]*@[A-Za-z0-9][A-Za-z0-9.-]*$")))
            throw new ArgumentException("Enter a valid UPI ID of at most 100 characters, such as pharmacy@bank.");
        if (payeeName is not null && (payeeName.Trim().Length > 100 || payeeName.Any(char.IsControl)))
            throw new ArgumentException("UPI payee name must fit 100 characters and contain no control characters.");
        if (!string.IsNullOrWhiteSpace(merchantCode) && !Regex.IsMatch(merchantCode.Trim(), @"\A[0-9]{4}\z"))
            throw new ArgumentException("Merchant category code must be the four digits supplied by your bank, or blank.");
    }

    /// <summary>
    /// Builds an unsigned payment URI within a completed invoice&apos;s outstanding balance; no receipt is recorded and the request reference is not a bank UTR.
    /// </summary>
    public static UpiPaymentRequestDto Create(SaleDto sale, PharmacySettingsDto profile, decimal? requestedAmount)
    {
        if (!profile.UpiEnabled) throw new InvalidOperationException("UPI QR collection is not enabled for this branch. Ask an administrator to configure Pharmacy Settings.");
        ValidateConfiguration(true, profile.UpiId, profile.UpiPayeeName, profile.UpiMerchantCode);
        if (sale.Status != "Completed" || sale.BalanceAmount <= 0m) throw new InvalidOperationException("Only completed invoices with an outstanding balance can request payment.");
        var amount = requestedAmount ?? sale.BalanceAmount;
        if (amount <= 0m || amount > sale.BalanceAmount || decimal.Round(amount, 2) != amount)
            throw new ArgumentException("UPI amount must be positive, within the outstanding balance, and have at most two decimal places.");
        var reference = Guid.NewGuid().ToString("N");
        var note = $"Invoice {sale.InvoiceNumber}";
        if (note.Length > 80) note = note[..80];
        var fields = new List<KeyValuePair<string, string>> {
            new("pa", profile.UpiId!.Trim()), new("pn", profile.UpiPayeeName!.Trim()),
            new("tr", reference), new("tn", note), new("am", amount.ToString("0.00", CultureInfo.InvariantCulture)), new("cu", "INR")
        };
        if (!string.IsNullOrWhiteSpace(profile.UpiMerchantCode)) fields.Add(new("mc", profile.UpiMerchantCode.Trim()));
        var uri = "upi://pay?" + string.Join("&", fields.Select(x => $"{x.Key}={Uri.EscapeDataString(x.Value)}"));
        // This is an unsigned payment request, never evidence of settlement. No ledger write occurs here.
        return new(sale.Id, sale.InvoiceNumber, profile.UpiId.Trim(), profile.UpiPayeeName.Trim(), amount, sale.BalanceAmount, reference, uri, DateTime.UtcNow);
    }
}

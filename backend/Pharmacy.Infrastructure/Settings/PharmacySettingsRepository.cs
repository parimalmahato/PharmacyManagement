using System.Data;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Auditing;
using Pharmacy.Application.Settings;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Settings;

/// <summary>
/// Persists validated, revision-checked branch settings and audit history without resetting issued invoice numbers.
/// </summary>
public sealed class PharmacySettingsRepository(PharmacyDbContext db, IAuditService audit) : IPharmacySettingsRepository
{
    /// <summary>
    /// Returns the active branch profile when configured, without exposing other branches&apos; settings.
    /// </summary>
    public async Task<PharmacySettingsDto?> GetAsync(CancellationToken ct = default)
    {
        var settings = await db.PharmacySettings.AsNoTracking().SingleOrDefaultAsync(ct);
        return settings is null ? null : SettingsManagement.ToDto(settings);
    }
    /// <summary>
    /// Validates and revision-checks branch profile changes while preserving the managed invoice sequence and recording an audit event.
    /// </summary>
    public async Task<PharmacySettingsDto> UpdateAsync(UpdatePharmacySettingsRequest request, CancellationToken ct = default)
    {
        SettingsManagement.Validate(request);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var s = await db.PharmacySettings.FromSqlRaw("SELECT * FROM dbo.PharmacySettings WITH (UPDLOCK, HOLDLOCK)").SingleOrDefaultAsync(ct);
        if (s is not null && request.Revision != s.Revision) throw new InvalidOperationException("Settings changed since loading. Reload the page before saving.");
        if (s is null && request.Revision is not null) throw new InvalidOperationException("Settings no longer exist. Reload the page.");
        var old = s is null ? null : SettingsManagement.ToDto(s);
        if (s is null) { s = new PharmacySettings(); db.PharmacySettings.Add(s); }
        s.PharmacyName = request.PharmacyName.Trim(); s.Address = request.Address.Trim(); s.PhoneNumber = request.PhoneNumber.Trim();
        s.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        s.Gstin = string.IsNullOrWhiteSpace(request.Gstin) ? null : request.Gstin.Trim().ToUpperInvariant();
        s.InvoicePrefix = request.InvoicePrefix.Trim().ToUpperInvariant(); s.GstEnabled = request.GstEnabled; s.DefaultGstPercentage = request.DefaultGstPercentage;
        s.InvoiceNumberDigits = request.InvoiceNumberDigits; s.LowStockThreshold = request.LowStockThreshold;
        s.UpiEnabled = request.UpiEnabled;
        s.UpiId = string.IsNullOrWhiteSpace(request.UpiId) ? null : request.UpiId.Trim();
        s.UpiPayeeName = string.IsNullOrWhiteSpace(request.UpiPayeeName) ? null : request.UpiPayeeName.Trim();
        s.UpiMerchantCode = string.IsNullOrWhiteSpace(request.UpiMerchantCode) ? null : request.UpiMerchantCode.Trim();
        s.ExpiryAlertsEnabled = request.ExpiryAlertsEnabled; s.ExpiryAlertDays = request.ExpiryAlertDays;
        s.ReceiptFooter = request.ReceiptFooter.Trim(); s.ReceiptShowCustomerPhone = request.ReceiptShowCustomerPhone;
        s.ReceiptShowPaymentHistory = request.ReceiptShowPaymentHistory; s.UpdatedAtUtc = DateTime.UtcNow; s.Revision = Guid.NewGuid();
        await db.SaveChangesAsync(ct);
        var result = SettingsManagement.ToDto(s);
        await audit.WriteAsync("PharmacySettingsUpdated", "PharmacySettings", s.Id.ToString(), oldValues: old, newValues: result, saveImmediately: false, cancellationToken: ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return result;
    }
}

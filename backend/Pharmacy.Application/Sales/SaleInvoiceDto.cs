
using Pharmacy.Application.Settings;

namespace Pharmacy.Application.Sales;

/// <summary>
/// Printable invoice data combining preserved sale information with the current branch profile and settlement history.
/// </summary>
public sealed record SaleInvoiceDto(
    SaleDto Sale,
    PharmacySettingsDto Pharmacy
);

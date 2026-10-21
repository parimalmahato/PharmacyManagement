
using Pharmacy.Domain.Sales;

namespace Pharmacy.Domain.Entities.Sales;

/// <summary>
/// Captures the sold medicine, original batch and GST-inclusive price calculation as invoice-time snapshots.
/// </summary>
public class SaleItem
{
    private SaleItem() { } // EF Core

    public int Id { get; private set; }
    public int SaleId { get; private set; }
    public int MedicineId { get; private set; }
    public int MedicineBatchId { get; private set; }

    public string MedicineNameSnapshot { get; private set; }
        = string.Empty;

    public string? HsnCode { get; private set; }

    public int Quantity { get; private set; }

    // Original GST-inclusive unit price
    /// <summary>
    /// Original GST-inclusive unit selling price captured when this invoice line was issued.
    /// </summary>
    public decimal UnitPrice { get; private set; }

    public decimal GstPercentage { get; private set; }
    public decimal DiscountPercentage { get; private set; }

    public decimal GrossAmount { get; private set; }
    public decimal DiscountAmount { get; private set; }
    /// <summary>
    /// Original discounted line value excluding output GST, retained for historical reporting.
    /// </summary>
    public decimal TaxableAmount { get; private set; }
    /// <summary>
    /// Original rounded output GST captured with the issued line rather than recalculated from later catalog settings.
    /// </summary>
    public decimal GstAmount { get; private set; }

    // Final GST-inclusive amount after discount
    /// <summary>
    /// Original discounted GST-inclusive amount for this invoice line.
    /// </summary>
    public decimal LineTotal { get; private set; }

    public Sale Sale { get; private set; } = null!;

    public SaleItem(
        int medicineId,
        int medicineBatchId,
        int quantity,
        decimal unitPrice,
        string medicineName,
        string? hsnCode,
        decimal gstPercentage,
        decimal discountPercentage = 0m)
    {
        if (medicineId <= 0)
            throw new ArgumentException("Invalid medicine.");

        if (medicineBatchId <= 0)
            throw new ArgumentException("Invalid batch.");

        if (string.IsNullOrWhiteSpace(medicineName))
            throw new ArgumentException(
                "Medicine name is required.");

        var calculation =
            GstCalculator.CalculateInclusive(
                quantity,
                unitPrice,
                gstPercentage,
                discountPercentage);

        MedicineId = medicineId;
        MedicineBatchId = medicineBatchId;
        MedicineNameSnapshot = medicineName.Trim();
        HsnCode = hsnCode?.Trim();

        Quantity = quantity;
        UnitPrice = unitPrice;

        GstPercentage = gstPercentage;
        DiscountPercentage = discountPercentage;

        GrossAmount = calculation.GrossAmount;
        DiscountAmount = calculation.DiscountAmount;
        TaxableAmount = calculation.TaxableAmount;
        GstAmount = calculation.GstAmount;
        LineTotal = calculation.NetAmount;
    }
}

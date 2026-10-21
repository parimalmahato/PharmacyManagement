using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Reports;
using Pharmacy.Domain.Entities.Sales;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Reports;

/// <summary>
/// Builds branch report tables from invoice snapshots, dated credits and batch costs; current stock is not a historical snapshot.
/// </summary>
public sealed class AdvancedReportRepository(PharmacyDbContext db) : IAdvancedReportRepository
{
    private static ReportTable Table(string title, string[] columns, IEnumerable<object?[]> rows) =>
        new(title, columns, rows.Select(x => (IReadOnlyList<object?>)x).ToList());

    /// <summary>
    /// Builds the selected branch report with original invoice snapshots and dated credits; stock valuation is a current snapshot regardless of the selected historical period.
    /// </summary>
    public async Task<AdvancedReportDto> GetAsync(string kind, DateOnly start, DateOnly end, CancellationToken ct = default)
    {
        var (periodStart, until) = BusinessDates.Range(start, end);
        if (kind == "stock")
        {
            var today = BusinessDates.Today;
            var batches = await db.MedicineBatches.AsNoTracking().Include(x => x.Medicine).OrderBy(x => x.Medicine.Name).ThenBy(x => x.Id).ToListAsync(ct);
            var rows = batches.Select(x => new object?[] { x.Medicine.Name, x.BatchNumber, x.ExpiryDate.ToString("yyyy-MM-dd"), x.Quantity,
                x.PurchasePrice, x.SellingPrice, x.Quantity * x.PurchasePrice, x.Quantity * x.SellingPrice,
                !x.Medicine.IsActive ? "Inactive" : x.ExpiryDate <= today ? "Expired / unavailable" : "Saleable" });
            var eligible = batches.Where(x => x.Medicine.IsActive && x.ExpiryDate > today).ToList();
            return new(kind, start, end, "Current stock snapshot (IST), not historical stock. Retail value includes selling-price GST where applicable. Cost uses recorded batch purchase prices.",
                [Table("Valuation summary", ["Metric", "INR"], [ ["All stock at cost", batches.Sum(x => x.Quantity * x.PurchasePrice)], ["Saleable stock at cost", eligible.Sum(x => x.Quantity * x.PurchasePrice)], ["Saleable stock at retail", eligible.Sum(x => x.Quantity * x.SellingPrice)] ]),
                 Table("Batch valuation", ["Medicine", "Batch", "Expiry", "Quantity", "Unit cost", "Unit retail", "Cost value", "Retail value", "Status"], rows)]);
        }
        var lines = await (from item in db.SaleItems.AsNoTracking()
                           join sale in db.Sales on item.SaleId equals sale.Id
                           join batch in db.MedicineBatches on item.MedicineBatchId equals batch.Id
                           where (sale.Status == SaleStatus.Completed || sale.Status == SaleStatus.Cancelled) && sale.CreatedAtUtc < until &&
                             (sale.CreatedAtUtc >= periodStart || (sale.CancelledAtUtc >= periodStart && sale.CancelledAtUtc < until) || db.SaleReturnItems.Any(ri => ri.SaleItemId == item.Id &&
                               db.SaleReturns.Any(rh => rh.Id == ri.SaleReturnId && rh.CreatedAtUtc >= periodStart && rh.CreatedAtUtc < until)))
                           select new { Item = item, sale.InvoiceNumber, sale.CreatedAtUtc, sale.CancelledAtUtc, sale.CancellationStockRestored, batch.PurchasePrice, batch.BatchNumber }).ToListAsync(ct);
        var originals = lines.ToDictionary(x => x.Item.Id);
        var originalIds = originals.Keys.ToList();
        var returnRows = await (from item in db.SaleReturnItems.AsNoTracking()
                                join header in db.SaleReturns on item.SaleReturnId equals header.Id
                                where header.CreatedAtUtc < until && originalIds.Contains(item.SaleItemId)
                                orderby header.CreatedAtUtc, header.Id, item.Id
                                select new { Item = item, header.CreatedAtUtc }).ToListAsync(ct);
        var credits = new List<Credit>();
        var previous = new Dictionary<int, decimal>();
        foreach (var row in returnRows)
        {
            if (!originals.TryGetValue(row.Item.SaleItemId, out var original)) continue;
            var prior = previous.GetValueOrDefault(row.Item.SaleItemId);
            var total = prior + row.Item.Amount;
            var gst = original.Item.LineTotal == 0 ? 0 : Round(total * original.Item.GstAmount / original.Item.LineTotal)
                - Round(prior * original.Item.GstAmount / original.Item.LineTotal);
            previous[row.Item.SaleItemId] = total;
            if (row.CreatedAtUtc >= periodStart)
                credits.Add(new(original.Item, row.Item.Quantity, row.Item.Amount, gst, row.Item.Restock ? row.Item.Quantity * original.PurchasePrice : 0m));
        }
        foreach (var line in lines.Where(x => x.CancelledAtUtc >= periodStart && x.CancelledAtUtc < until))
            credits.Add(new(line.Item, line.Item.Quantity, line.Item.LineTotal, line.Item.GstAmount,
                line.CancellationStockRestored ? line.Item.Quantity * line.PurchasePrice : 0m));
        var sold = lines.Where(x => x.CreatedAtUtc >= periodStart).ToList();
        var tables = new List<ReportTable>();
        var notes = "India Standard Time. Original invoices remain on their issue dates; return/cancellation credits use their own transaction dates. Refunds are cash movements, not an additional reduction of sales. Cancellation cost is recovered only when stock was restored. Values in INR.";
        if (kind == "sales")
        {
            var summary = await new ReportRepository(db).GetSalesReportAsync(start, end, ct);
            tables.Add(Table("Sales summary", ["Metric", "INR"], [ ["Invoiced sales", summary.TotalRevenue], ["Return / cancellation credits", summary.ReturnCredits], ["Net sales", summary.NetSales], ["Collections", summary.Collections], ["Refunds", summary.Refunds], ["Net collections", summary.NetCollections], ["Current cohort outstanding", summary.OutstandingBalance] ]));
            tables.Add(Table("Invoices", ["Invoice", "IST date", "Subtotal", "Discount", "Taxable", "GST", "Total"],
                sold.GroupBy(x => x.Item.SaleId).Select(g => new object?[] { g.First().InvoiceNumber, g.First().CreatedAtUtc.AddMinutes(330).ToString("yyyy-MM-dd"),
                    g.Sum(x => x.Item.GrossAmount), g.Sum(x => x.Item.DiscountAmount), g.Sum(x => x.Item.TaxableAmount), g.Sum(x => x.Item.GstAmount), g.Sum(x => x.Item.LineTotal) })));
            tables.Add(Table("Daily activity", ["Date", "Sales", "Invoiced", "Credits", "Net sales", "Collections", "Refunds"], summary.DailySales.Select(x => new object?[] { x.Date.ToString("yyyy-MM-dd"), x.SalesCount, x.Revenue, x.ReturnCredits, x.NetSales, x.Collections, x.Refunds })));
        }
        else if (kind == "gst")
        {
            var keys = sold.Select(x => (Hsn: x.Item.HsnCode ?? "Unclassified", Rate: x.Item.GstPercentage))
                .Concat(credits.Select(x => (Hsn: x.Original.HsnCode ?? "Unclassified", Rate: x.Original.GstPercentage))).Distinct().OrderBy(x => x.Hsn).ThenBy(x => x.Rate);
            tables.Add(Table("GST by HSN and rate", ["HSN", "GST %", "Taxable sales", "GST sales", "Taxable credits", "GST credits", "Net taxable", "Net GST"], keys.Select(key =>
            {
                var s = sold.Where(x => (x.Item.HsnCode ?? "Unclassified") == key.Hsn && x.Item.GstPercentage == key.Rate).ToList();
                var r = credits.Where(x => (x.Original.HsnCode ?? "Unclassified") == key.Hsn && x.Original.GstPercentage == key.Rate).ToList();
                var taxable = s.Sum(x => x.Item.TaxableAmount); var gst = s.Sum(x => x.Item.GstAmount);
                var creditGst = r.Sum(x => x.Gst); var creditTaxable = r.Sum(x => x.Amount - x.Gst);
                return new object?[] { key.Hsn, key.Rate, taxable, gst, creditTaxable, creditGst, taxable - creditTaxable, gst - creditGst };
            })));
            notes += " GST is allocated from original invoice snapshots; return GST uses cumulative credit rounding. No CGST/SGST/IGST or input-tax-credit classification is inferred from missing place-of-supply/purchase-tax data. This is an internal GST summary.";
        }
        else if (kind == "medicines")
        {
            var ids = sold.Select(x => x.Item.MedicineId).Concat(credits.Select(x => x.Original.MedicineId)).Distinct().OrderBy(x => x);
            tables.Add(Table("Medicine-wise activity", ["Medicine ID", "Medicine", "Sold qty", "Returned qty", "Net qty", "Invoiced", "Credits", "Net sales"], ids.Select(id =>
            {
                var s = sold.Where(x => x.Item.MedicineId == id).ToList(); var r = credits.Where(x => x.Original.MedicineId == id).ToList();
                return new object?[] { id, s.FirstOrDefault()?.Item.MedicineNameSnapshot ?? r.First().Original.MedicineNameSnapshot, s.Sum(x => x.Item.Quantity), r.Sum(x => x.Quantity),
                    s.Sum(x => x.Item.Quantity) - r.Sum(x => x.Quantity), s.Sum(x => x.Item.LineTotal), r.Sum(x => x.Amount), s.Sum(x => x.Item.LineTotal) - r.Sum(x => x.Amount) };
            })));
        }
        else if (kind == "profit")
        {
            var taxable = sold.Sum(x => x.Item.TaxableAmount); var cost = sold.Sum(x => x.Item.Quantity * x.PurchasePrice);
            var revenueCredit = credits.Sum(x => x.Amount - x.Gst); var recoveredCost = credits.Sum(x => x.RecoveredCost);
            tables.Add(Table("Gross profit", ["Metric", "INR"], [ ["Taxable sales", taxable], ["Taxable return/cancellation credits", revenueCredit], ["Net taxable sales", taxable - revenueCredit],
                ["Cost of goods sold", cost], ["Restocked cost recovered", recoveredCost], ["Net cost of goods", cost - recoveredCost], ["Gross profit", taxable - revenueCredit - cost + recoveredCost] ]));
            tables.Add(Table("Sold batch costs", ["Invoice", "Medicine", "Batch", "Quantity", "Unit cost", "Taxable sales", "Cost", "Gross profit"], sold.Select(x => new object?[] {
                x.InvoiceNumber, x.Item.MedicineNameSnapshot, x.BatchNumber, x.Item.Quantity, x.PurchasePrice, x.Item.TaxableAmount, x.Item.Quantity * x.PurchasePrice, x.Item.TaxableAmount - x.Item.Quantity * x.PurchasePrice })));
            notes += " Gross profit uses original batch purchase cost and sales excluding output GST. Only restocked returns reverse cost; damaged/non-restocked returns retain their cost. Operating expenses and purchase input-tax adjustments are not available, so this is not net profit.";
        }
        else throw new ArgumentException("Unknown report type.");
        return new(kind, start, end, notes, tables);
    }
    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private sealed record Credit(SaleItem Original, int Quantity, decimal Amount, decimal Gst, decimal RecoveredCost);
}

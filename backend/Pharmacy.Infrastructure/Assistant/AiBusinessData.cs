using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Assistant;
using Pharmacy.Application.Reports;
using Pharmacy.Domain.Entities.Sales;
using Pharmacy.Domain.Entities.Purchasing;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Assistant;

// All business queries use the existing branch-filtered DbContext. No model-generated SQL is executed.
/// <summary>
/// Reads existing branch-filtered inventory, demand and purchase data for AI-assisted screens; model output never becomes executable SQL.
/// </summary>
/// <remarks>
/// Operational queries use the injected DbContext's current branch filters. The medicine catalog is
/// intentionally shared. These reads are independent snapshots, not a reservation or posting transaction.
/// </remarks>
public sealed class AiBusinessData(PharmacyDbContext db) : IAiBusinessData
{
    /// <summary>
    /// Returns at most 100 active-medicine batches using a fixed stock, expiry or low-stock query. Search is explicit; expiry results can include unsaleable expired stock.
    /// </summary>
    /// <param name="kind">One of stock, expiry or low-stock; other values are rejected.</param>
    /// <param name="search">Explicit medicine-name or batch substring, or an empty string for no search filter.</param>
    /// <param name="days">Calendar-day expiry horizon relative to the India business date.</param>
    /// <param name="ct">Cancels the database reads when the requesting operation ends.</param>
    /// <returns>Positive-quantity batch facts ordered by earliest expiry, without purchase costs.</returns>
    /// <exception cref="ArgumentException">The query kind is not supported.</exception>
    public async Task<IReadOnlyList<InventoryFact>> InventoryAsync(string kind, string search, int days, CancellationToken ct)
    {
        var today = BusinessDates.Today;
        var until = today.AddDays(days);
        var threshold = await db.PharmacySettings.Select(s => (int?)s.LowStockThreshold).FirstOrDefaultAsync(ct) ?? 10;
        var query = db.MedicineBatches.AsNoTracking().Where(b => b.Medicine.IsActive && b.Quantity > 0);
        if (search.Length > 0) query = query.Where(b => b.Medicine.Name.Contains(search) || b.BatchNumber.Contains(search));
        query = kind switch
        {
            "expiry" => query.Where(b => b.ExpiryDate <= until),
            "low-stock" => query.Where(b => b.ExpiryDate > today && b.Quantity <= threshold),
            "stock" => query.Where(b => b.ExpiryDate > today),
            _ => throw new ArgumentException("Unsupported inventory query.")
        };
        return await query.OrderBy(b => b.ExpiryDate).ThenBy(b => b.Id).Take(100)
            .Select(b => new InventoryFact(b.MedicineId, b.Medicine.Name, b.BatchNumber, b.ExpiryDate, b.Quantity)).ToListAsync(ct);
    }

    /// <summary>
    /// Reads the shared active catalog for exact-name suggestions; a matching name still requires human verification of medicine identity and units.
    /// </summary>
    /// <param name="ct">Cancels the catalog read.</param>
    /// <returns>Active medicine identifiers and names shared across branches.</returns>
    public async Task<IReadOnlyList<(int Id, string Name)>> MedicinesAsync(CancellationToken ct) =>
        (await db.Medicines.AsNoTracking().Where(m => m.IsActive).Select(m => new { m.Id, m.Name }).ToListAsync(ct))
        .Select(m => (m.Id, m.Name)).ToArray();

    /// <summary>
    /// Aggregates branch stock and recent non-cancelled sale quantities less returns against those lines; only draft supply expiring beyond the coverage horizon reduces suggested purchases.
    /// </summary>
    /// <param name="historyDays">Inclusive number of recent business calendar dates, including today, validated by the application service.</param>
    /// <param name="horizonDays">Coverage horizon used to separate soon-expiring stock and eligible expected supply.</param>
    /// <param name="ct">Cancels the sequential database reads.</param>
    /// <returns>Planning inputs for every active catalog medicine, including those with no branch stock or sales history.</returns>
    /// <remarks>
    /// Demand is a sale-date cohort: all recorded returns against those original lines reduce its demand,
    /// even when the return occurred later. Draft quantities are expected supply, not sellable inventory.
    /// </remarks>
    public async Task<IReadOnlyList<DemandFact>> DemandAsync(int historyDays, int horizonDays, CancellationToken ct)
    {
        var today = BusinessDates.Today;
        var (start, end) = BusinessDates.Range(today.AddDays(1 - historyDays), today);
        var expiry = today.AddDays(horizonDays);
        var stock = await db.MedicineBatches.AsNoTracking().Where(b => b.Medicine.IsActive && b.ExpiryDate > today)
            .GroupBy(b => b.MedicineId).Select(g => new { Id = g.Key, Available = g.Sum(b => b.Quantity), Expiring = g.Sum(b => b.ExpiryDate <= expiry ? b.Quantity : 0) }).ToListAsync(ct);
        var sales = await db.SaleItems.AsNoTracking().Where(i => i.Sale.Status == SaleStatus.Completed && i.Sale.CreatedAtUtc >= start && i.Sale.CreatedAtUtc < end)
            .Select(i => new { i.Id, i.MedicineId, i.Quantity }).ToListAsync(ct);
        var returned = await (from r in db.SaleReturnItems.AsNoTracking() join i in db.SaleItems on r.SaleItemId equals i.Id
            where i.Sale.Status == SaleStatus.Completed && i.Sale.CreatedAtUtc >= start && i.Sale.CreatedAtUtc < end
            group r by r.SaleItemId into g select new { Id = g.Key, Quantity = g.Sum(r => r.Quantity) }).ToDictionaryAsync(x => x.Id, x => x.Quantity, ct);
        var demand = sales.GroupBy(x => x.MedicineId).ToDictionary(g => g.Key, g => g.Sum(i => Math.Max(0, i.Quantity - returned.GetValueOrDefault(i.Id))));
        var pending = await (from item in db.PurchaseItems.AsNoTracking() join header in db.PurchaseInvoices on item.PurchaseInvoiceId equals header.Id
            where header.Status == PurchaseStatus.Draft && item.ExpiryDate > expiry group item by item.MedicineId into g
            select new { Id = g.Key, Quantity = g.Sum(i => i.Quantity) }).ToDictionaryAsync(x => x.Id, x => x.Quantity, ct);
        var byId = stock.ToDictionary(x => x.Id);
        var medicines = await MedicinesAsync(ct);
        return medicines.Select(m => new DemandFact(m.Id, m.Name, byId.GetValueOrDefault(m.Id)?.Available ?? 0,
            byId.GetValueOrDefault(m.Id)?.Expiring ?? 0, demand.GetValueOrDefault(m.Id), pending.GetValueOrDefault(m.Id))).ToArray();
    }
}

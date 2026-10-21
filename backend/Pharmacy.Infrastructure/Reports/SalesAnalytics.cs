using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Reports;
using Pharmacy.Domain.Entities.Payments;
using Pharmacy.Domain.Entities.Purchasing;
using Pharmacy.Domain.Entities.Sales;
using Pharmacy.Domain.Entities.Sales.Returns;

namespace Pharmacy.Infrastructure.Reports;

/// <summary>
/// Aggregates branch invoice, credit and cash events using their recorded dates while keeping current liabilities distinct from period activity.
/// </summary>
public sealed partial class ReportRepository
{
    private async Task<List<SaleSettlement>> Balances(IQueryable<Sale> sales, CancellationToken ct)
    {
        var rows = await sales.AsNoTracking().Select(s => new
        {
            TotalAmount = s.Status == SaleStatus.Cancelled ? 0m : s.TotalAmount,
            Paid = s.Payments.Sum(x => (decimal?)x.Amount) ?? 0m,
            Returned = s.Returns.SelectMany(x => x.Items).Sum(x => (decimal?)x.Amount) ?? 0m,
            Refunded = s.Refunds.Sum(x => (decimal?)x.Amount) ?? 0m
        }).ToListAsync(ct);
        return rows.Select(x => new SaleSettlement(x.TotalAmount, x.Paid, x.Returned, x.Refunded)).ToList();
    }

    /// <summary>
    /// Aggregates current branch dashboard activity and liabilities, distinguishing invoice revenue from cash collections and refund payouts.
    /// </summary>
    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken cancellationToken = default)
    {
        var today = BusinessDates.Today;
        var threshold = await _dbContext.PharmacySettings.Select(x => (int?)x.LowStockThreshold).SingleOrDefaultAsync(cancellationToken) ?? 10;
        var report = await GetSalesReportAsync(today, today, cancellationToken);
        var balances = await Balances(_dbContext.Sales.Where(x => x.Status == SaleStatus.Completed || x.Status == SaleStatus.Cancelled), cancellationToken);
        var activeStock = _dbContext.MedicineBatches.Where(x => x.Medicine.IsActive && x.ExpiryDate > today);
        var expiryLimit = today.AddDays(30);
        return new(report.TotalRevenue, report.TotalSales,
            await _dbContext.Medicines.CountAsync(x => x.IsActive, cancellationToken),
            await activeStock.CountAsync(x => x.Quantity > 0 && x.Quantity <= threshold, cancellationToken))
        {
            TodayReturnCredits = report.ReturnCredits, TodayCollections = report.Collections, TodayRefunds = report.Refunds,
            LowStockThreshold = threshold,
            OutstandingBalance = balances.Sum(x => x.BalanceAmount), RefundDue = balances.Sum(x => x.RefundDue),
            TodayReceivedPurchases = report.ReceivedPurchases, TodayPurchaseValue = report.PurchaseValue,
            DraftPurchases = await _dbContext.PurchaseInvoices.CountAsync(x => x.Status == PurchaseStatus.Draft, cancellationToken),
            OutOfStockMedicines = await _dbContext.Medicines.CountAsync(x => x.IsActive && !activeStock.Any(b => b.MedicineId == x.Id && b.Quantity > 0), cancellationToken),
            ExpiringWithin30Days = await activeStock.CountAsync(x => x.Quantity > 0 && x.ExpiryDate <= expiryLimit, cancellationToken),
            InventoryCostValue = await activeStock.SumAsync(x => (decimal?)(x.Quantity * x.PurchasePrice), cancellationToken) ?? 0m
        };
    }

    /// <summary>
    /// Builds dated sales and cash activity using India business-date UTC boundaries; credits retain their own event dates.
    /// </summary>
    public async Task<SalesReportDto> GetSalesReportAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        var (start, end) = BusinessDates.Range(startDate, endDate);
        var sales = _dbContext.Sales.Where(x => (x.Status == SaleStatus.Completed || x.Status == SaleStatus.Cancelled) && x.CreatedAtUtc >= start && x.CreatedAtUtc < end);
        var receipts = _dbContext.Payments.Where(x => x.CreatedAtUtc >= start && x.CreatedAtUtc < end);
        var refunds = _dbContext.SaleRefunds.Where(x => x.CreatedAtUtc >= start && x.CreatedAtUtc < end);
        var credits = from item in _dbContext.SaleReturnItems
                      join header in _dbContext.SaleReturns on item.SaleReturnId equals header.Id
                      where header.CreatedAtUtc >= start && header.CreatedAtUtc < end
                      select new { header.CreatedAtUtc, item.Amount };
        var cancellations = _dbContext.Sales.Where(x => x.Status == SaleStatus.Cancelled && x.CancelledAtUtc >= start && x.CancelledAtUtc < end);
        var activity = sales.Select(x => new { Date = x.CreatedAtUtc.AddMinutes(330).Date, Count = 1, Gross = x.TotalAmount, Credit = 0m, Collected = 0m, Refunded = 0m })
            .Concat(cancellations.Select(x => new { Date = x.CancelledAtUtc!.Value.AddMinutes(330).Date, Count = 0, Gross = 0m, Credit = x.TotalAmount, Collected = 0m, Refunded = 0m }))
            .Concat(receipts.Select(x => new { Date = x.CreatedAtUtc.AddMinutes(330).Date, Count = 0, Gross = 0m, Credit = 0m, Collected = x.Amount, Refunded = 0m }))
            .Concat(refunds.Select(x => new { Date = x.CreatedAtUtc.AddMinutes(330).Date, Count = 0, Gross = 0m, Credit = 0m, Collected = 0m, Refunded = x.Amount }))
            .Concat(credits.Select(x => new { Date = x.CreatedAtUtc.AddMinutes(330).Date, Count = 0, Gross = 0m, Credit = x.Amount, Collected = 0m, Refunded = 0m }));
        var grouped = await activity.GroupBy(x => x.Date).Select(g => new
        {
            Date = g.Key, Count = g.Sum(x => x.Count), Gross = g.Sum(x => x.Gross),
            Credit = g.Sum(x => x.Credit), Collected = g.Sum(x => x.Collected), Refunded = g.Sum(x => x.Refunded)
        }).ToListAsync(cancellationToken);
        var days = grouped.ToDictionary(x => DateOnly.FromDateTime(x.Date));
        var daily = Enumerable.Range(0, endDate.DayNumber - startDate.DayNumber + 1).Select(offset =>
        {
            var date = startDate.AddDays(offset);
            days.TryGetValue(date, out var day);
            return new DailySalesDto(date, day?.Count ?? 0, day?.Gross ?? 0m)
            { ReturnCredits = day?.Credit ?? 0m, Collections = day?.Collected ?? 0m, Refunds = day?.Refunded ?? 0m };
        }).ToList();
        // Rankings intentionally show original invoiced volume, independent of later returns.
        var top = await (from item in _dbContext.SaleItems
                         join sale in sales on item.SaleId equals sale.Id
                         join medicine in _dbContext.Medicines on item.MedicineId equals medicine.Id
                         group item by new { item.MedicineId, medicine.Name } into g
                         orderby g.Sum(x => x.Quantity) descending, g.Key.Name, g.Key.MedicineId
                         select new TopSellingMedicineDto(g.Key.MedicineId, g.Key.Name, g.Sum(x => x.Quantity), g.Sum(x => x.LineTotal)))
            .Take(10).ToListAsync(cancellationToken);
        var paymentMethods = await receipts.GroupBy(x => x.Method).Select(g => new { Method = g.Key, Amount = g.Sum(x => x.Amount) }).ToListAsync(cancellationToken);
        var refundMethods = await refunds.GroupBy(x => x.Method).Select(g => new { Method = g.Key, Amount = g.Sum(x => x.Amount) }).ToListAsync(cancellationToken);
        var purchases = _dbContext.PurchaseInvoices.Where(x => x.Status == PurchaseStatus.Received && x.InvoiceDate >= startDate && x.InvoiceDate <= endDate);
        var balances = await Balances(sales, cancellationToken);
        return new(startDate, endDate, daily.Sum(x => x.SalesCount), daily.Sum(x => x.Revenue), daily, top)
        {
            ReturnCredits = daily.Sum(x => x.ReturnCredits), Collections = daily.Sum(x => x.Collections), Refunds = daily.Sum(x => x.Refunds),
            OutstandingBalance = balances.Sum(x => x.BalanceAmount), RefundDue = balances.Sum(x => x.RefundDue),
            ReceivedPurchases = await purchases.CountAsync(cancellationToken), PurchaseValue = await purchases.SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0m,
            CollectionsByMethod = Enum.GetValues<PaymentMethod>().Select(method => new CollectionMethodDto(method.ToString(),
                paymentMethods.FirstOrDefault(x => x.Method == method)?.Amount ?? 0m,
                refundMethods.FirstOrDefault(x => (int)x.Method == (int)method)?.Amount ?? 0m)).ToList()
        };
    }
}

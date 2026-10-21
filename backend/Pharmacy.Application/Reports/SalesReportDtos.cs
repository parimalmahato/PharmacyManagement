namespace Pharmacy.Application.Reports;

/// <summary>
/// Branch dashboard metrics separating invoice revenue, credits, collections, refunds, liabilities and stock alerts.
/// </summary>
public sealed record DashboardSummaryDto(
    decimal TodayRevenue,
    int TodaySalesCount,
    int TotalActiveMedicines,
    int LowStockBatchCount
)
{
    public decimal TodayReturnCredits { get; init; }
    public int LowStockThreshold { get; init; } = 10;
    public decimal TodayNetSales => TodayRevenue - TodayReturnCredits;
    public decimal TodayCollections { get; init; }
    public decimal TodayRefunds { get; init; }
    public decimal TodayNetCollections => TodayCollections - TodayRefunds;
    public decimal OutstandingBalance { get; init; }
    public decimal RefundDue { get; init; }
    public int TodayReceivedPurchases { get; init; }
    public decimal TodayPurchaseValue { get; init; }
    public int DraftPurchases { get; init; }
    public int OutOfStockMedicines { get; init; }
    public int ExpiringWithin30Days { get; init; }
    public decimal InventoryCostValue { get; init; }
}

/// <summary>
/// Business-date invoice and cash activity; net sales and net collections represent different measures.
/// </summary>
public sealed record DailySalesDto(
    DateOnly Date,
    int SalesCount,
    decimal Revenue
)
{
    public decimal ReturnCredits { get; init; }
    public decimal NetSales => Revenue - ReturnCredits;
    public decimal Collections { get; init; }
    public decimal Refunds { get; init; }
    public decimal NetCollections => Collections - Refunds;
}

/// <summary>
/// Medicine-level sales quantity and revenue used in branch sales analytics.
/// </summary>
public sealed record TopSellingMedicineDto(
    int MedicineId,
    string MedicineName,
    int QuantitySold,
    decimal Revenue
);

/// <summary>
/// A bounded branch sales-period report with dated invoice/credit activity, receipts, refunds and current cohort liabilities.
/// </summary>
public sealed record SalesReportDto(
    DateOnly StartDate,
    DateOnly EndDate,
    int TotalSales,
    decimal TotalRevenue,
    List<DailySalesDto> DailySales,
    List<TopSellingMedicineDto> TopSellingMedicines
)
{
    public decimal ReturnCredits { get; init; }
    public decimal NetSales => TotalRevenue - ReturnCredits;
    public decimal Collections { get; init; }
    public decimal Refunds { get; init; }
    public decimal NetCollections => Collections - Refunds;
    public decimal OutstandingBalance { get; init; }
    public decimal RefundDue { get; init; }
    public int ReceivedPurchases { get; init; }
    public decimal PurchaseValue { get; init; }
    public IReadOnlyList<CollectionMethodDto> CollectionsByMethod { get; init; } = [];
}

/// <summary>
/// Receipt and payout totals for one payment method, with their net cash movement.
/// </summary>
public sealed record CollectionMethodDto(string Method, decimal Collections, decimal Refunds)
{
    public decimal NetCollections => Collections - Refunds;
}

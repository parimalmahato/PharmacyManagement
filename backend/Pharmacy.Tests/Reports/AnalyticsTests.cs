using Pharmacy.Application.Reports;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Reports;

namespace Pharmacy.Tests.Reports;

public sealed class AnalyticsTests
{
    [Theory]
    [InlineData("sales")]
    [InlineData("gst")]
    [InlineData("medicines")]
    [InlineData("profit")]
    [InlineData("stock")]
    public async Task AdvancedReportQueriesTranslateBeforeOpeningSqlConnection(string kind)
    {
        var options = new DbContextOptionsBuilder<PharmacyDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=True;TrustServerCertificate=True")
            .AddInterceptors(new PreventConnection()).Options;
        await using var db = new PharmacyDbContext(options);
        await Assert.ThrowsAsync<ConnectionPrevented>(() => new AdvancedReportRepository(db)
            .GetAsync(kind, new DateOnly(2026, 10, 21), new DateOnly(2026, 10, 21)));
    }
    [Fact]
    public async Task CombinedDailyQueryTranslatesBeforeOpeningSqlConnection()
    {
        var options = new DbContextOptionsBuilder<PharmacyDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=True;TrustServerCertificate=True")
            .AddInterceptors(new PreventConnection()).Options;
        await using var db = new PharmacyDbContext(options);
        await Assert.ThrowsAsync<ConnectionPrevented>(() => new ReportRepository(db)
            .GetSalesReportAsync(new DateOnly(2026, 10, 21), new DateOnly(2026, 10, 21)));
    }

    private sealed class ConnectionPrevented : Exception { }
    private sealed class PreventConnection : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
            throw new ConnectionPrevented();
    }
    [Fact]
    public void IndiaBusinessDayStartsOnPreviousUtcDate()
    {
        var range = BusinessDates.Range(new DateOnly(2026, 10, 21), new DateOnly(2026, 10, 21));
        Assert.Equal(new DateTime(2026, 10, 20, 18, 30, 0, DateTimeKind.Utc), range.StartUtc);
        Assert.Equal(new DateTime(2026, 10, 21, 18, 30, 0, DateTimeKind.Utc), range.EndUtc);
    }

    [Fact]
    public void RejectsMissingReversedAndExcessiveRanges()
    {
        Assert.Throws<ArgumentException>(() => BusinessDates.Range(DateOnly.MinValue, new DateOnly(2026, 1, 1)));
        Assert.Throws<ArgumentException>(() => BusinessDates.Range(new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 1)));
        Assert.Throws<ArgumentException>(() => BusinessDates.Range(new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 2)));
        Assert.Throws<ArgumentException>(() => BusinessDates.Range(DateOnly.MaxValue, DateOnly.MaxValue));
    }

    [Fact]
    public void NetSalesAndCashFlowRemainSeparateAndCanBeNegative()
    {
        var day = new DailySalesDto(new DateOnly(2026, 10, 21), 0, 0)
        { ReturnCredits = 40, Collections = 10, Refunds = 25 };
        Assert.Equal(-40m, day.NetSales);
        Assert.Equal(-15m, day.NetCollections);
    }
}

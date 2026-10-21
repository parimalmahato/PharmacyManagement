
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Reports;
using Pharmacy.Domain.Entities.Sales;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Reports;

/// <summary>
/// Aggregates branch invoice, credit and cash events using their recorded dates while keeping current liabilities distinct from period activity.
/// </summary>
public sealed partial class ReportRepository : IReportRepository
{
    private const int LowStockThreshold = 10;

    private readonly PharmacyDbContext _dbContext;

    public ReportRepository(PharmacyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Reads branch batch alerts using the configured low-stock threshold.
    /// </summary>
    public async Task<List<LowStockBatchDto>> GetLowStockBatchesAsync(
        CancellationToken cancellationToken = default)
    {
        var lowStockThreshold = await _dbContext.PharmacySettings.Select(x => (int?)x.LowStockThreshold).SingleOrDefaultAsync(cancellationToken) ?? 10;

        var today = BusinessDates.Today;

        return await (
            from batch in _dbContext.MedicineBatches.AsNoTracking()
            join medicine in _dbContext.Medicines.AsNoTracking()
                on batch.MedicineId equals medicine.Id
            where medicine.IsActive
                && batch.Quantity > 0
                && batch.Quantity <= lowStockThreshold
                && batch.ExpiryDate > today
            orderby batch.Quantity, medicine.Name
            select new LowStockBatchDto(
                batch.Id,
                medicine.Id,
                medicine.Name,
                batch.BatchNumber,
                batch.Quantity,
                batch.ExpiryDate
            )
        ).ToListAsync(cancellationToken);
    }
    
    /// <summary>
    /// Builds branch expiry summaries and batch lists for staff review without automatically changing stock.
    /// </summary>
    public async Task<ExpiryReportDto> GetExpiryReportAsync(
        CancellationToken cancellationToken = default)
    {
        // Use India Standard Time for pharmacy business dates.
        var indiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
            "Asia/Kolkata");

        var indiaNow = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow,
            indiaTimeZone);

        var today = DateOnly.FromDateTime(indiaNow);

        var endDate = today.AddDays(90);

        // Fetch expired and upcoming expiry batches.
        // Include only batches with stock remaining.
        var batches = await (
            from batch in _dbContext.MedicineBatches.AsNoTracking()
            join medicine in _dbContext.Medicines.AsNoTracking()
                on batch.MedicineId equals medicine.Id
            where batch.Quantity > 0
                && batch.ExpiryDate <= endDate
            orderby batch.ExpiryDate, medicine.Name
            select new
            {
                batch.Id,
                batch.MedicineId,
                MedicineName = medicine.Name,
                batch.BatchNumber,
                batch.ExpiryDate,
                batch.Quantity
            }
        ).ToListAsync(cancellationToken);

        var result = batches.Select(batch =>
        {
            var daysUntilExpiry =
                batch.ExpiryDate.DayNumber - today.DayNumber;

            var status = daysUntilExpiry < 0
                ? "Expired"
                : daysUntilExpiry == 0
                    ? "Expires Today"
                    : daysUntilExpiry <= 30
                        ? "Expiring Within 30 Days"
                        : daysUntilExpiry <= 60
                            ? "Expiring Within 60 Days"
                            : "Expiring Within 90 Days";

            return new ExpiringBatchDto(
                batch.Id,
                batch.MedicineId,
                batch.MedicineName,
                batch.BatchNumber,
                batch.ExpiryDate,
                batch.Quantity,
                daysUntilExpiry,
                status
            );
        }).ToList();

        var summary = new ExpirySummaryDto(
            ExpiredBatches: result.Count(x => x.DaysUntilExpiry < 0),
            ExpiringWithin30Days: result.Count(x =>
                x.DaysUntilExpiry >= 0 &&
                x.DaysUntilExpiry <= 30),
            ExpiringWithin60Days: result.Count(x =>
                x.DaysUntilExpiry >= 0 &&
                x.DaysUntilExpiry <= 60),
            ExpiringWithin90Days: result.Count(x =>
                x.DaysUntilExpiry >= 0 &&
                x.DaysUntilExpiry <= 90)
        );

        return new ExpiryReportDto(summary, result);
    }


}

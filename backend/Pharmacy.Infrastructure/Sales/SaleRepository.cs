
using System.Data;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Auditing;
using Pharmacy.Application.Sales;
using Pharmacy.Domain.Entities;
using Pharmacy.Domain.Entities.Sales;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Sales;

/// <summary>
/// Coordinates transactional checkout and cancellation with original-batch stock changes, invoice snapshots and retry protection.
/// </summary>
public sealed partial class SaleRepository(
    PharmacyDbContext dbContext,
    IAuditService auditService) : ISaleRepository
{
    /// <summary>
    /// Allocates eligible batches and commits invoice snapshots, stock consumption and audit history atomically, reusing matching checkout retry identities.
    /// </summary>
    public async Task<SaleDto> CreateAsync(
        CreateSaleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var fingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(request with { IdempotencyKey = null }))));
        async Task<SaleDto> Replay(Sale saved)
        {
            if (saved.CheckoutFingerprint != fingerprint) throw new InvalidOperationException("Checkout key was already used with different details.");
            return await GetByIdAsync(saved.Id, cancellationToken) ?? throw new InvalidOperationException("Saved checkout could not be retrieved.");
        }
        if (request.IdempotencyKey.HasValue)
        {
            if (request.IdempotencyKey == Guid.Empty) throw new ArgumentException("Invalid checkout key.");
            var saved = await dbContext.Sales.AsNoTracking().SingleOrDefaultAsync(x => x.CheckoutKey == request.IdempotencyKey, cancellationToken);
            if (saved is not null) return await Replay(saved);
        }
        if (request.CustomerId.HasValue)
        {
            var customerExists = await dbContext.Customers
                .AnyAsync(
                    x => x.Id == request.CustomerId.Value
                         && x.IsActive,
                    cancellationToken);

            if (!customerExists)
            {
                throw new KeyNotFoundException(
                    $"Active customer with ID " +
                    $"{request.CustomerId.Value} was not found.");
            }
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            throw new ArgumentException(
                "At least one medicine is required.");
        }

        if (request.Items.Any(x =>
                x is null || x.MedicineId <= 0 || x.Quantity <= 0))
        {
            throw new ArgumentException(
                "Medicine IDs and quantities must be positive.");
        }

        if (request.Items
            .Select(x => x.MedicineId)
            .Distinct()
            .Count() != request.Items.Count)
        {
            throw new ArgumentException(
                "Each medicine can appear only once in a sale.");
        }

        if (request.Items.Any(x =>
                x.DiscountPercentage < 0m ||
                x.DiscountPercentage > 100m))
        {
            throw new ArgumentException(
                "Discount percentage must be between 0 and 100.");
        }

        // Begin transaction.
        // Serializable isolation protects concurrent stock allocation.
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var medicineIds = request.Items
            .Select(x => x.MedicineId)
            .ToList();
        if (request.IdempotencyKey is Guid checkoutKey)
        {
            var saved = await dbContext.Sales.FromSqlInterpolated($"SELECT * FROM dbo.Sales WITH (UPDLOCK, HOLDLOCK) WHERE CheckoutKey = {checkoutKey}")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            if (saved is not null) return await Replay(saved);
        }

        var activeMedicineIds = await dbContext.Medicines
            .Where(x =>
                medicineIds.Contains(x.Id) && x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (activeMedicineIds.Count != medicineIds.Count)
        {
            throw new ArgumentException(
                "One or more medicines do not exist or are inactive.");
        }

        // Load tax classification inside the same transaction.
        var medicineDetails = await dbContext.Medicines
            .AsNoTracking()
            .Where(x => medicineIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var settings = await dbContext.PharmacySettings
            .FromSqlRaw("SELECT * FROM dbo.PharmacySettings WITH (UPDLOCK, HOLDLOCK)")
            .SingleOrDefaultAsync(cancellationToken);
        string invoiceNumber;
        if (settings is null) invoiceNumber = $"SAL-{Guid.NewGuid():N}";
        else
        {
            if (settings.NextInvoiceNumber <= 0 || settings.NextInvoiceNumber == long.MaxValue)
                throw new InvalidOperationException("Invoice sequence is exhausted or invalid.");
            invoiceNumber = $"{settings.InvoicePrefix}-{settings.NextInvoiceNumber.ToString($"D{settings.InvoiceNumberDigits}")}";
            settings.NextInvoiceNumber++;
            if (await dbContext.Sales.AnyAsync(x => x.InvoiceNumber == invoiceNumber, cancellationToken))
                throw new InvalidOperationException("The next invoice number already exists. Review invoice configuration.");
        }

        var sale = new Sale(invoiceNumber);
        if (request.IdempotencyKey is Guid key) sale.IdentifyCheckout(key, fingerprint);

        if (request.CustomerId.HasValue)
        {
            sale.AssignCustomer(request.CustomerId.Value);
        }

        // Save sale header to generate Sale.Id.
        // This SaveChanges is still inside the transaction.
        dbContext.Sales.Add(sale);

        await dbContext.SaveChangesAsync(cancellationToken);

        var today = Pharmacy.Domain.BusinessClock.Today;

        foreach (var requestedItem in request.Items)
        {
            // Lock eligible batches and allocate stock using FEFO.
            var batches = await dbContext.MedicineBatches
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM dbo.MedicineBatches WITH (UPDLOCK, HOLDLOCK)
                    WHERE MedicineId = {requestedItem.MedicineId}
                      AND Quantity > 0
                      AND ExpiryDate > {today}
                    """)
                .OrderBy(x => x.ExpiryDate).ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);

            var available = batches.Sum(x => x.Quantity);

            if (available < requestedItem.Quantity)
            {
                throw new InvalidOperationException(
                    $"Insufficient unexpired stock for medicine " +
                    $"{requestedItem.MedicineId}. " +
                    $"Available: {available}.");
            }

            var remaining = requestedItem.Quantity;

            foreach (var batch in batches)
            {
                if (remaining == 0)
                {
                    break;
                }

                var quantityToSell =
                    Math.Min(remaining, batch.Quantity);

                // Domain validation and stock deduction.
                batch.RemoveStock(quantityToSell);

                // Add sale item.
                var medicine = medicineDetails[requestedItem.MedicineId];

                sale.AddItem(new SaleItem(
                    medicineId: requestedItem.MedicineId,
                    medicineBatchId: batch.Id,
                    quantity: quantityToSell,
                    unitPrice: batch.SellingPrice,
                    medicineName: medicine.Name,
                    hsnCode: medicine.HsnCode,
                    gstPercentage: settings is { GstEnabled: false } ? 0m : medicine.GstPercentage,
                    discountPercentage: requestedItem.DiscountPercentage));

                // Record inventory movement.
                dbContext.StockMovements.Add(
                    new StockMovement(
                        batch.Id,
                        StockMovementType.Sale,
                        -quantityToSell,
                        $"Sale:{sale.Id}",
                        $"Invoice {invoiceNumber}"));

                remaining -= quantityToSell;
            }
        }

        // Save sale items, inventory deductions,
        // and stock movements inside the transaction.
        await dbContext.SaveChangesAsync(cancellationToken);

        // Audit logging: add the audit record to the
        // same scoped DbContext without saving immediately.
        await auditService.WriteAsync(
            action: "SaleCompleted",
            entityType: "Sale",
            entityId: sale.Id.ToString(),
            newValues: new
            {
                sale.InvoiceNumber,
                sale.CustomerId,
                sale.SubtotalAmount,
                sale.DiscountAmount,
                sale.TaxableAmount,
                sale.GstAmount,
                sale.TotalAmount,
                ItemCount = sale.Items.Count,
                Items = sale.Items.Select(x => new
                {
                    x.MedicineId,
                    x.MedicineBatchId,
                    x.Quantity,
                    x.UnitPrice,
                    x.MedicineNameSnapshot,
                    x.HsnCode,
                    x.GstPercentage,
                    x.DiscountPercentage,
                    x.GrossAmount,
                    x.DiscountAmount,
                    x.TaxableAmount,
                    x.GstAmount,
                    x.LineTotal
                }).ToList()
            },
            saveImmediately: false,
            cancellationToken: cancellationToken
        );

        // Save audit log in the SAME SQL transaction.
        await dbContext.SaveChangesAsync(cancellationToken);

        // Commit everything together:
        // Sale + SaleItems + stock deductions +
        // StockMovements + AuditLog.
        await transaction.CommitAsync(cancellationToken);

        return await GetByIdAsync(sale.Id, cancellationToken)
            ?? throw new InvalidOperationException(
                "Sale was saved but could not be retrieved.");
    }

    /// <summary>
    /// Returns branch sale history with original totals and current payment, credit and cancellation settlement information.
    /// </summary>
    public async Task<IReadOnlyList<SaleDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var sales = await dbContext.Sales
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Payments)
            .Include(x => x.Returns).ThenInclude(x => x.Items)
            .Include(x => x.Refunds)
            .AsSplitQuery()
            .Include(x => x.Items)
            .OrderByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        var medicineIds = sales
            .SelectMany(x => x.Items)
            .Select(x => x.MedicineId)
            .Distinct()
            .ToList();

        var medicineNames = await dbContext.Medicines
            .AsNoTracking()
            .Where(x => medicineIds.Contains(x.Id))
            .ToDictionaryAsync(
                x => x.Id,
                x => x.Name,
                cancellationToken);

        var batchIds = sales
            .SelectMany(x => x.Items)
            .Select(x => x.MedicineBatchId)
            .Distinct()
            .ToList();

        var batchDetails = await dbContext.MedicineBatches
            .AsNoTracking()
            .Where(x => batchIds.Contains(x.Id))
            .ToDictionaryAsync(
                x => x.Id,
                x => (x.BatchNumber, x.ExpiryDate),
                cancellationToken);

        return sales
            .Select(s => Map(
                s,
                medicineNames,
                batchDetails))
            .ToList();
    }

   /// <summary>
   /// Returns a branch-visible sale and its settlement view or null when it cannot be found.
   /// </summary>
   public async Task<SaleDto?> GetByIdAsync(
    int id,
    CancellationToken cancellationToken = default)
    {
        var sale = await dbContext.Sales
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Payments)
            .Include(x => x.Returns).ThenInclude(x => x.Items)
            .Include(x => x.Refunds)
            .AsSplitQuery()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (sale is null)
        {
            return null;
        }

        var medicineIds = sale.Items
            .Select(x => x.MedicineId)
            .Distinct()
            .ToList();

        var medicineNames = await dbContext.Medicines
            .AsNoTracking()
            .Where(x => medicineIds.Contains(x.Id))
            .ToDictionaryAsync(
                x => x.Id,
                x => x.Name,
                cancellationToken);

        var batchIds = sale.Items
            .Select(x => x.MedicineBatchId)
            .Distinct()
            .ToList();

        var batchDetails = await dbContext.MedicineBatches
            .AsNoTracking()
            .Where(x => batchIds.Contains(x.Id))
            .ToDictionaryAsync(
                x => x.Id,
                x => (x.BatchNumber, x.ExpiryDate),
                cancellationToken);

        return Map(
            sale,
            medicineNames,
            batchDetails);
    }

    private static SaleDto Map(
        Sale sale,
        IReadOnlyDictionary<int, string> medicineNames,
        IReadOnlyDictionary<int, (string BatchNumber, DateOnly ExpiryDate)> batchDetails)
    {
        var items = sale.Items.Select(x =>
        {
            var batch = batchDetails.GetValueOrDefault(
                x.MedicineBatchId,
                ("Unknown", DateOnly.MinValue));

            var medicineName = !string.IsNullOrWhiteSpace(x.MedicineNameSnapshot)
                ? x.MedicineNameSnapshot
                : medicineNames.GetValueOrDefault(x.MedicineId, "Unknown");

            return new SaleItemDto(
                x.MedicineId,
                medicineName,
                batch.Item1,
                batch.Item2,
                x.Quantity,
                x.UnitPrice,
                x.LineTotal,
                x.HsnCode,
                x.GstPercentage,
                x.DiscountPercentage,
                x.GrossAmount,
                x.DiscountAmount,
                x.TaxableAmount,
                x.GstAmount);
        }).ToList();

        return new SaleDto(
            sale.Id,
            sale.InvoiceNumber,
            sale.CreatedAtUtc,
            sale.TotalAmount,
            sale.Status.ToString(),
            items,
            sale.CustomerId,
            sale.Customer?.Name.ToString(),
            sale.Customer?.Phone,
            sale.SubtotalAmount,
            sale.DiscountAmount,
            sale.TaxableAmount,
            sale.GstAmount)
        {
            CancelledAtUtc = sale.CancelledAtUtc, CancelledByUserId = sale.CancelledByUserId,
            CancellationReason = sale.CancellationReason, CancellationStockRestored = sale.CancellationStockRestored,
            PaidAmount = sale.Payments.Sum(x => x.Amount),
            ReturnedAmount = sale.Returns.Sum(x => x.TotalAmount),
            RefundedAmount = sale.Refunds.Sum(x => x.Amount)
        };
    }
}

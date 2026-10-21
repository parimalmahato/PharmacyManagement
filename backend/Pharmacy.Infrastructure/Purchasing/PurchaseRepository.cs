
using System.Data;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Auditing;
using Pharmacy.Application.Purchasing;
using Pharmacy.Domain.Entities;
using Pharmacy.Domain.Entities.Purchasing;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Purchasing;

/// <summary>
/// Creates branch purchase drafts and atomically receives batches and stock movements, preventing repeated receiving.
/// </summary>
public sealed class PurchaseRepository(
    PharmacyDbContext dbContext,
    IAuditService auditService) : IPurchaseRepository
{
    /// <summary>
    /// Reads supplier invoices and items belonging to the active branch.
    /// </summary>
    public Task<List<PurchaseInvoice>> GetAllAsync(
        CancellationToken cancellationToken) =>
        dbContext.PurchaseInvoices
            .AsNoTracking()
            .Include(x => x.Items)
            .OrderByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Loads one branch-visible supplier invoice or returns null when absent.
    /// </summary>
    public Task<PurchaseInvoice?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken) =>
        dbContext.PurchaseInvoices
            .Include(x => x.Items)
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

    /// <summary>
    /// Checks that an eligible supplier exists before a purchase draft can be created.
    /// </summary>
    public Task<bool> SupplierExistsAsync(
        int supplierId,
        CancellationToken cancellationToken) =>
        dbContext.Suppliers.AnyAsync(
            x => x.Id == supplierId && x.IsActive,
            cancellationToken);

    /// <summary>
    /// Checks every requested medicine against the active shared catalog before accepting purchase lines.
    /// </summary>
    public async Task<bool> ActiveMedicinesExistAsync(
        IEnumerable<int> medicineIds,
        CancellationToken cancellationToken)
    {
        var ids = medicineIds.Distinct().ToArray();

        return ids.Length > 0 &&
               await dbContext.Medicines.CountAsync(
                   x => ids.Contains(x.Id) && x.IsActive,
                   cancellationToken) == ids.Length;
    }

    /// <summary>
    /// Saves a validated supplier-invoice draft with audit information without receiving its quantities into stock.
    /// </summary>
    public async Task AddAsync(
        PurchaseInvoice purchase,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        try
        {
            // Step 1: Add purchase invoice.
            await dbContext.PurchaseInvoices.AddAsync(
                purchase,
                cancellationToken);

            // Step 2: Save invoice and generate ID.
            // The transaction is not committed yet.
            await dbContext.SaveChangesAsync(
                cancellationToken);

            // Step 3: Add purchase creation audit record.
            await auditService.WriteAsync(
                action: "PurchaseCreated",
                entityType: "PurchaseInvoice",
                entityId: purchase.Id.ToString(),
                newValues: new
                {
                    purchase.SupplierId,
                    purchase.InvoiceNumber,
                    purchase.InvoiceDate,
                    purchase.TotalAmount,
                    Status = purchase.Status.ToString(),
                    ItemCount = purchase.Items.Count
                },
                saveImmediately: false,
                cancellationToken: cancellationToken);

            // Step 4: Save audit record.
            await dbContext.SaveChangesAsync(
                cancellationToken);

            // Step 5: Commit invoice and audit together.
            await transaction.CommitAsync(
                cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(
                CancellationToken.None);
            throw;
        }
    }

    /// <summary>
    /// Validates an unreceived draft and atomically posts its batch quantities, stock movements, received status and audit history.
    /// </summary>
    public async Task ReceiveAsync(
        int purchaseId,
        CancellationToken cancellationToken)
    {
        // Serializable isolation helps protect concurrent
        // receiving operations affecting shared batches.
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        try
        {
            // Lock purchase invoice row.
            var purchase = await dbContext.PurchaseInvoices
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM dbo.PurchaseInvoices WITH (UPDLOCK, HOLDLOCK)
                    WHERE Id = {purchaseId}
                    """)
                .Include(x => x.Items)
                .FirstOrDefaultAsync(cancellationToken);

            if (purchase is null)
            {
                throw new KeyNotFoundException(
                    "Purchase invoice not found.");
            }

            if (purchase.Status != PurchaseStatus.Draft)
            {
                throw new InvalidOperationException(
                    "Only a draft purchase can be received.");
            }

            var medicineIds = purchase.Items
                .Select(x => x.MedicineId)
                .Distinct()
                .ToArray();

            var activeMedicineIds = await dbContext.Medicines
                .Where(x =>
                    medicineIds.Contains(x.Id) && x.IsActive)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            if (activeMedicineIds.Count != medicineIds.Length)
            {
                throw new InvalidOperationException(
                    "One or more medicines are missing or inactive.");
            }

            var today = DateOnly.FromDateTime(
                DateTime.UtcNow);

            foreach (var item in purchase.Items)
            {
                // Step 1: Validate expiry date.
                if (item.ExpiryDate <= today)
                {
                    throw new InvalidOperationException(
                        $"Batch {item.BatchNumber} is expired.");
                }

                // Step 2: Lock matching batch for update.
                // The serializable transaction also protects
                // against concurrent insertion of the same batch.
                var batch = await dbContext.MedicineBatches
                    .FromSqlInterpolated($"""
                        SELECT *
                        FROM dbo.MedicineBatches
                        WITH (UPDLOCK, HOLDLOCK)
                        WHERE MedicineId = {item.MedicineId}
                          AND BatchNumber = {item.BatchNumber}
                        """)
                    .FirstOrDefaultAsync(cancellationToken);

                if (batch is not null)
                {
                    // Step 3: Validate existing batch details.
                    if (batch.ExpiryDate != item.ExpiryDate ||
                        batch.PurchasePrice != item.PurchasePrice ||
                        batch.SellingPrice != item.SellingPrice)
                    {
                        throw new InvalidOperationException(
                            $"Batch {item.BatchNumber} already exists " +
                            "with different expiry or price details.");
                    }

                    // Step 4: Increase existing batch stock.
                    batch.AddStock(item.Quantity);
                }
                else
                {
                    // Step 5: Create new medicine batch.
                    batch = new MedicineBatch(
                        item.MedicineId,
                        item.BatchNumber,
                        item.ExpiryDate,
                        item.PurchasePrice,
                        item.SellingPrice,
                        item.Quantity);

                    await dbContext.MedicineBatches.AddAsync(
                        batch,
                        cancellationToken);
                }

                // Save batch so new batches have generated IDs.
                // Still inside the active transaction.
                await dbContext.SaveChangesAsync(
                    cancellationToken);

                // Step 6: Record stock movement.
                dbContext.StockMovements.Add(
                    new StockMovement(
                        batch.Id,
                        StockMovementType.PurchaseReceived,
                        item.Quantity,
                        $"Purchase:{purchase.Id}",
                        $"Supplier invoice: {purchase.InvoiceNumber}"));
            }

            // Step 7: Mark purchase as received.
            purchase.MarkReceived();

            // Step 8: Save stock movements and status.
            await dbContext.SaveChangesAsync(
                cancellationToken);

            // Step 9: Add audit log within transaction.
            await auditService.WriteAsync(
                action: "PurchaseReceived",
                entityType: "PurchaseInvoice",
                entityId: purchase.Id.ToString(),
                newValues: new
                {
                    purchase.SupplierId,
                    purchase.InvoiceNumber,
                    purchase.TotalAmount,
                    Status = purchase.Status.ToString(),
                    Items = purchase.Items.Select(x => new
                    {
                        x.MedicineId,
                        x.BatchNumber,
                        x.ExpiryDate,
                        x.Quantity,
                        x.PurchasePrice,
                        x.SellingPrice
                    }).ToList()
                },
                saveImmediately: false,
                cancellationToken: cancellationToken);

            // Step 10: Save audit log.
            await dbContext.SaveChangesAsync(
                cancellationToken);

            // Step 11: Commit all changes atomically.
            await transaction.CommitAsync(
                cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(
                CancellationToken.None);
            throw;
        }
    }
}

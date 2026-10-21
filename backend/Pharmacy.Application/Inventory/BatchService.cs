
using Pharmacy.Application.Auditing;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Inventory;

/// <summary>
/// Registers validated opening-stock batches and their audit events within a repository transaction.
/// </summary>
public sealed class BatchService(
    IBatchRepository repository,
    IAuditService auditService) : IBatchService
{
    /// <summary>
    /// Maps branch batch records to inventory display DTOs and their calculated alert flags.
    /// </summary>
    public async Task<IReadOnlyList<BatchDto>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        var batches = await repository.GetAllAsync(
            cancellationToken);

        return batches.Select(ToDto).ToList();
    }

    /// <summary>
    /// Checks the active medicine, constructs a valid batch and commits opening stock with its audit entry before reloading the response.
    /// </summary>
    public async Task<BatchDto> CreateAsync(
        CreateBatchRequest request,
        CancellationToken cancellationToken)
    {
        // Step 1: Validate medicine.
        var medicineExists =
            await repository.MedicineExistsAsync(
                request.MedicineId,
                cancellationToken);

        if (!medicineExists)
        {
            throw new KeyNotFoundException(
                "The medicine does not exist or is inactive.");
        }

        var batch = new MedicineBatch(
            request.MedicineId,
            request.BatchNumber,
            request.ExpiryDate,
            request.PurchasePrice,
            request.SellingPrice,
            request.Quantity);

        // Step 2: Create batch and audit in one transaction.
        await repository.ExecuteInTransactionAsync(
            async ct =>
            {
                // Add batch.
                await repository.AddAsync(
                    batch,
                    ct);

                // Save batch to generate its ID.
                // Transaction is still active.
                await repository.SaveChangesAsync(ct);

                // Write audit record using generated batch ID.
                await auditService.WriteAsync(
                    action: "BatchCreated",
                    entityType: "MedicineBatch",
                    entityId: batch.Id.ToString(),
                    newValues: new
                    {
                        batch.MedicineId,
                        batch.BatchNumber,
                        batch.ExpiryDate,
                        batch.PurchasePrice,
                        batch.SellingPrice,
                        batch.Quantity
                    },
                    saveImmediately: false,
                    cancellationToken: ct);
            },
            cancellationToken);

        // Step 3: Load navigation property for response.
        var savedBatch = await repository.GetByIdAsync(
            batch.Id,
            cancellationToken);

        if (savedBatch is null)
        {
            throw new InvalidOperationException(
                "Batch was saved but could not be retrieved.");
        }

        return ToDto(savedBatch);
    }

    private static BatchDto ToDto(MedicineBatch batch)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return new BatchDto(
            batch.Id,
            batch.MedicineId,
            batch.Medicine.Name,
            batch.BatchNumber,
            batch.ExpiryDate,
            batch.PurchasePrice,
            batch.SellingPrice,
            batch.Quantity,
            batch.ExpiryDate < today,
            batch.ExpiryDate >= today &&
                batch.ExpiryDate <= today.AddDays(30),
            batch.Quantity <= 10);
    }
}

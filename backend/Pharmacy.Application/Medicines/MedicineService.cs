using Pharmacy.Application.Auditing;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Medicines;

/// <summary>
/// Validates medicine-catalog changes and coordinates persistence and auditing through application contracts.
/// </summary>
public sealed class MedicineService(
    IMedicineRepository repository,
    IAuditService auditService,
    Pharmacy.Application.Settings.IPharmacySettingsRepository? settingsRepository = null) : IMedicineService
{
    /// <summary>
    /// Maps shared catalog records into application DTOs for the medicine screen.
    /// </summary>
    public async Task<IReadOnlyList<MedicineDto>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        var medicines = await repository.GetAllAsync(cancellationToken);
        return medicines.Select(ToDto).ToList();
    }

    /// <summary>
    /// Returns the requested medicine DTO or null when the catalog entry cannot be found.
    /// </summary>
    public async Task<MedicineDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var medicine = await repository.GetByIdAsync(id, cancellationToken);
        return medicine is null ? null : ToDto(medicine);
    }

    /// <summary>
    /// Validates and saves a new shared medicine with its audit event through the repository transaction boundary.
    /// </summary>
    public async Task<MedicineDto> CreateAsync(
        CreateMedicineRequest request,
        CancellationToken cancellationToken)
    {
        var defaults = request.GstPercentage is null && settingsRepository is not null ? await settingsRepository.GetAsync(cancellationToken) : null;
        var medicine = new Medicine(
            request.Name,
            request.GenericName,
            request.Manufacturer,
            request.Category,
            request.HsnCode,
            request.GstPercentage ?? (defaults is { GstEnabled: true } ? defaults.DefaultGstPercentage : 0m));

        await repository.ExecuteInTransactionAsync(
            async ct =>
            {
                await repository.AddAsync(medicine, ct);
                await repository.SaveChangesAsync(ct);

                await auditService.WriteAsync(
                    action: "MedicineCreated",
                    entityType: "Medicine",
                    entityId: medicine.Id.ToString(),
                    newValues: Snapshot(medicine),
                    saveImmediately: false,
                    cancellationToken: ct);
            },
            cancellationToken);

        return ToDto(medicine);
    }

    /// <summary>
    /// Updates an existing catalog entry and audit history without rewriting issued invoice snapshots; returns null when absent.
    /// </summary>
    public async Task<MedicineDto?> UpdateAsync(
        int id,
        UpdateMedicineRequest request,
        CancellationToken cancellationToken)
    {
        MedicineDto? result = null;

        await repository.ExecuteInTransactionAsync(
            async ct =>
            {
                var medicine = await repository.GetByIdAsync(id, ct);
                if (medicine is null) return;

                var oldValues = Snapshot(medicine);

                medicine.Update(
                    request.Name,
                    request.GenericName,
                    request.Manufacturer,
                    request.Category);

                medicine.ConfigureTax(
                    request.HsnCode,
                    request.GstPercentage);

                await auditService.WriteAsync(
                    action: "MedicineUpdated",
                    entityType: "Medicine",
                    entityId: medicine.Id.ToString(),
                    oldValues: oldValues,
                    newValues: Snapshot(medicine),
                    saveImmediately: false,
                    cancellationToken: ct);

                result = ToDto(medicine);
            },
            cancellationToken);

        return result;
    }

    /// <summary>
    /// Soft-deactivates an existing catalog entry and records the change rather than deleting historical references.
    /// </summary>
    public async Task<bool> DeactivateAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var success = false;

        await repository.ExecuteInTransactionAsync(
            async ct =>
            {
                var medicine = await repository.GetByIdAsync(id, ct);
                if (medicine is null) return;

                var oldValues = new { medicine.IsActive };
                medicine.Deactivate();

                await auditService.WriteAsync(
                    action: "MedicineDeactivated",
                    entityType: "Medicine",
                    entityId: medicine.Id.ToString(),
                    oldValues: oldValues,
                    newValues: new { medicine.IsActive },
                    saveImmediately: false,
                    cancellationToken: ct);

                success = true;
            },
            cancellationToken);

        return success;
    }

    private static object Snapshot(Medicine medicine) => new
    {
        medicine.Name,
        medicine.GenericName,
        medicine.Manufacturer,
        medicine.Category,
        medicine.IsActive,
        medicine.HsnCode,
        medicine.GstPercentage
    };

    private static MedicineDto ToDto(Medicine medicine) => new(
        medicine.Id,
        medicine.Name,
        medicine.GenericName,
        medicine.Manufacturer,
        medicine.Category,
        medicine.IsActive,
        medicine.HsnCode,
        medicine.GstPercentage);
}

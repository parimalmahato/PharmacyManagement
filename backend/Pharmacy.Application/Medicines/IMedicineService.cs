namespace Pharmacy.Application.Medicines;

/// <summary>
/// Defines medicine-catalog use cases exposed to the API without exposing persistence details.
/// </summary>
public interface IMedicineService
{
    /// <summary>
    /// Maps shared catalog records into application DTOs for the medicine screen.
    /// </summary>
    Task<IReadOnlyList<MedicineDto>> GetAllAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the requested medicine DTO or null when the catalog entry cannot be found.
    /// </summary>
    Task<MedicineDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    /// <summary>
    /// Validates and saves a new shared medicine with its audit event through the repository transaction boundary.
    /// </summary>
    Task<MedicineDto> CreateAsync(
        CreateMedicineRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing catalog entry and audit history without rewriting issued invoice snapshots; returns null when absent.
    /// </summary>
    Task<MedicineDto?> UpdateAsync(
        int id,
        UpdateMedicineRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Soft-deactivates an existing catalog entry and records the change rather than deleting historical references.
    /// </summary>
    Task<bool> DeactivateAsync(
        int id,
        CancellationToken cancellationToken);
}
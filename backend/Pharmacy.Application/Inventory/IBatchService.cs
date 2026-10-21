namespace Pharmacy.Application.Inventory;

/// <summary>
/// Defines branch inventory viewing and opening-stock registration use cases.
/// </summary>
public interface IBatchService
{
    /// <summary>
    /// Maps branch batch records to inventory display DTOs and their calculated alert flags.
    /// </summary>
    Task<IReadOnlyList<BatchDto>> GetAllAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Checks the active medicine, constructs a valid batch and commits opening stock with its audit entry before reloading the response.
    /// </summary>
    Task<BatchDto> CreateAsync(
        CreateBatchRequest request,
        CancellationToken cancellationToken);
}

namespace Pharmacy.Application.Sales;

/// <summary>
/// Defines branch checkout, invoice reads and cancellation while preserving original invoice and payment history.
/// </summary>
public interface ISaleRepository
{
    /// <summary>
    /// Validates custody, existing adjustments and optional inspection before atomically preserving cancellation and any original-batch reversal; paid amounts become refund liabilities.
    /// </summary>
    Task<SaleDto> CancelAsync(int id, CancelSaleRequest request, string actorId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Allocates eligible batches and commits invoice snapshots, stock consumption and audit history atomically, reusing matching checkout retry identities.
    /// </summary>
    Task<SaleDto> CreateAsync(
        CreateSaleRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns branch sale history with original totals and current payment, credit and cancellation settlement information.
    /// </summary>
    Task<IReadOnlyList<SaleDto>> GetAllAsync(
        CancellationToken cancellationToken = default);
    /// <summary>
    /// Returns a branch-visible sale and its settlement view or null when it cannot be found.
    /// </summary>
    Task<SaleDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);
}

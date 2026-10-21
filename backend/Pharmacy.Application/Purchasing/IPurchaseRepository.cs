using Pharmacy.Domain.Entities.Purchasing;

namespace Pharmacy.Application.Purchasing;

/// <summary>
/// Defines supplier-invoice draft storage, catalog checks and receiving stock for the current branch.
/// </summary>
public interface IPurchaseRepository
{
    /// <summary>
    /// Reads supplier invoices and items belonging to the active branch.
    /// </summary>
    Task<List<PurchaseInvoice>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Loads one branch-visible supplier invoice or returns null when absent.
    /// </summary>
    Task<PurchaseInvoice?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    /// <summary>
    /// Checks that an eligible supplier exists before a purchase draft can be created.
    /// </summary>
    Task<bool> SupplierExistsAsync(
        int supplierId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Checks every requested medicine against the active shared catalog before accepting purchase lines.
    /// </summary>
    Task<bool> ActiveMedicinesExistAsync(
        IEnumerable<int> medicineIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Saves a validated supplier-invoice draft with audit information without receiving its quantities into stock.
    /// </summary>
    Task AddAsync(
        PurchaseInvoice purchase,
        CancellationToken cancellationToken);

    /// <summary>
    /// Validates an unreceived draft and atomically posts its batch quantities, stock movements, received status and audit history.
    /// </summary>
    Task ReceiveAsync(
        int purchaseId,
        CancellationToken cancellationToken);
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using Pharmacy.Application.Authorization;
using Pharmacy.Application.Purchasing;
using Pharmacy.Domain.Entities.Purchasing;

namespace Pharmacy.Api.Controllers;

/// <summary>
/// Validates supplier-invoice drafts and exposes explicit receiving as the operation that changes branch inventory.
/// </summary>
[ApiController]
[Route("api/purchases")]
[Authorize(Policy = ApplicationPolicies.ManagePurchasing)]
public sealed class PurchasesController : ControllerBase
{
    private readonly IPurchaseRepository _repository;

    public PurchasesController(
        IPurchaseRepository purchaseRepository)
    {
        _repository = purchaseRepository;
    }

    // ==========================================
    // GET: api/purchases
    // Get all purchase invoices
    // ==========================================
    /// <summary>
    /// Lists supplier invoices belonging to the active branch.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<PurchaseDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var purchases = await _repository.GetAllAsync(
            cancellationToken);

        var result = purchases
            .Select(ToDto)
            .ToList();

        return Ok(result);
    }

    // ==========================================
    // GET: api/purchases/{id}
    // Get purchase invoice by ID
    // ==========================================
    /// <summary>
    /// Returns a branch-visible purchase and its original item values or a not-found response.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PurchaseDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var purchase = await _repository.GetByIdAsync(
            id,
            cancellationToken);

        if (purchase is null)
        {
            return NotFound(new
            {
                message = "Purchase invoice not found."
            });
        }

        return Ok(ToDto(purchase));
    }

    // ==========================================
    // POST: api/purchases
    // Create a new purchase invoice
    // ==========================================
    /// <summary>
    /// Validates supplier and medicine lines before creating an unreceived purchase draft; inventory remains unchanged.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PurchaseDto>> Create(
        [FromBody] CreatePurchaseRequest request,
        CancellationToken cancellationToken)
    {
        // Validate purchase items.
        if (request.Items is null ||
            request.Items.Count == 0)
        {
            return BadRequest(new
            {
                message = "Add at least one purchase item."
            });
        }

        // Prevent duplicate medicine/batch combinations.
        if (request.Items
            .GroupBy(x => new
            {
                x.MedicineId,
                x.BatchNumber
            })
            .Any(g => g.Count() > 1))
        {
            return BadRequest(new
            {
                message =
                    "Each medicine and batch combination " +
                    "must appear only once."
            });
        }

        try
        {
            // Step 1: Validate supplier.
            var supplierExists =
                await _repository.SupplierExistsAsync(
                    request.SupplierId,
                    cancellationToken);

            if (!supplierExists)
            {
                return BadRequest(new
                {
                    message = "Supplier is missing or inactive."
                });
            }

            // Step 2: Validate medicines.
            var medicineIds = request.Items
                .Select(x => x.MedicineId)
                .ToArray();

            var medicinesExist =
                await _repository.ActiveMedicinesExistAsync(
                    medicineIds,
                    cancellationToken);

            if (!medicinesExist)
            {
                return BadRequest(new
                {
                    message =
                        "One or more medicines are missing or inactive."
                });
            }

            // Step 3: Create purchase invoice.
            var purchase = new PurchaseInvoice(
                request.SupplierId,
                request.InvoiceNumber,
                request.InvoiceDate);

            // Step 4: Add purchase items.
            foreach (var item in request.Items)
            {
                purchase.AddItem(
                    new PurchaseItem(
                        item.MedicineId,
                        item.BatchNumber,
                        item.ExpiryDate,
                        item.Quantity,
                        item.PurchasePrice,
                        item.SellingPrice));
            }

            // Step 5: Save purchase invoice.
            // Repository also writes PurchaseCreated audit.
            await _repository.AddAsync(
                purchase,
                cancellationToken);

            // Step 6: Return created invoice.
            return CreatedAtAction(
                nameof(GetById),
                new { id = purchase.Id },
                ToDto(purchase));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                message = ex.Message
            });
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException sqlException &&
                  (sqlException.Number == 2601 ||
                   sqlException.Number == 2627))
        {
            return Conflict(new
            {
                message =
                    "This invoice number already exists " +
                    "for this supplier."
            });
        }
    }

    // ==========================================
    // POST: api/purchases/{id}/receive
    // Receive medicines and update stock
    // ==========================================
    /// <summary>
    /// Receives a validated draft through the repository&apos;s atomic batch, movement and status update workflow.
    /// </summary>
    [HttpPost("{id:int}/receive")]
    public async Task<IActionResult> Receive(
        int id,
        CancellationToken cancellationToken)
    {
        try
        {
            // Repository handles:
            // 1. Purchase validation
            // 2. Medicine batch creation/update
            // 3. Stock movements
            // 4. Purchase status update
            // 5. PurchaseReceived audit logging
            // 6. SQL transaction commit
            await _repository.ReceiveAsync(
                id,
                cancellationToken);

            return Ok(new
            {
                message =
                    "Purchase received and stock updated successfully.",
                purchaseId = id
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new
            {
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                message = ex.Message
            });
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException sqlException &&
                  (sqlException.Number == 2601 ||
                   sqlException.Number == 2627))
        {
            return Conflict(new
            {
                message =
                    "A conflicting medicine batch already exists."
            });
        }
    }

    // ==========================================
    // Map PurchaseInvoice entity to PurchaseDto
    // ==========================================
    private static PurchaseDto ToDto(
        PurchaseInvoice purchase)
    {
        return new PurchaseDto(
            purchase.Id,
            purchase.SupplierId,
            purchase.InvoiceNumber,
            purchase.InvoiceDate,
            purchase.Status.ToString(),
            purchase.TotalAmount,
            purchase.Items.Select(item =>
                new PurchaseItemDto(
                    item.MedicineId,
                    item.BatchNumber,
                    item.ExpiryDate,
                    item.Quantity,
                    item.PurchasePrice,
                    item.SellingPrice))
                .ToList());
    }
}


using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using Pharmacy.Application.Inventory;
using Pharmacy.Application.Authorization;

namespace Pharmacy.Api.Controllers;

/// <summary>
/// Exposes inventory reads and authorized opening-stock registration through the batch application service.
/// </summary>
[ApiController]
[Route("api/inventory/batches")]
public sealed class InventoryController : ControllerBase
{
    private readonly IBatchService _batchService;

    public InventoryController(IBatchService batchService)
    {
        _batchService = batchService;
    }

    // ==========================================
    // GET: api/inventory/batches
    // Get all medicine batches
    // ==========================================
    /// <summary>
    /// Returns the active branch&apos;s batch inventory display data.
    /// </summary>
    [Authorize(Policy = ApplicationPolicies.ViewInventory)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BatchDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var batches = await _batchService.GetAllAsync(
            cancellationToken);

        return Ok(batches);
    }

    // ==========================================
    // POST: api/inventory/batches
    // Create a new medicine batch
    // ==========================================
    /// <summary>
    /// Registers legitimate opening stock through the transactional batch service for authorized inventory staff.
    /// </summary>
    [Authorize(Policy = ApplicationPolicies.ManageInventory)]
    [HttpPost]
    public async Task<ActionResult<BatchDto>> Create(
        [FromBody] CreateBatchRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // BatchService handles:
            // 1. Medicine validation
            // 2. Batch creation
            // 3. Database transaction
            // 4. BatchCreated audit logging
            // 5. Transaction commit

            var batch = await _batchService.CreateAsync(
                request,
                cancellationToken);

            // Return HTTP 201 Created.
            // Use the existing collection endpoint as the
            // Location until a GetById API is implemented.
            return Created(
                "/api/inventory/batches",
                batch);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
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
            when (ex.InnerException is SqlException
            {
                Number: 2601 or 2627
            })
        {
            return Conflict(new
            {
                message =
                    "This batch number already exists for this medicine."
            });
        }
    }
}


using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Pharmacy.Application.Authorization;
using Pharmacy.Application.Medicines;

namespace Pharmacy.Api.Controllers;

/// <summary>
/// Exposes medicine-catalog reads and authorized maintenance through the medicine application service.
/// </summary>
[ApiController]
[Route("api/medicines")]
public sealed class MedicinesController : ControllerBase
{
    private readonly IMedicineService _medicineService;

    public MedicinesController(
        IMedicineService medicineService)
    {
        _medicineService = medicineService;
    }

    /// <summary>
    /// Returns shared medicine-catalog records to authorized viewers.
    /// </summary>
    [Authorize(Policy = ApplicationPolicies.ViewMedicines)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MedicineDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var medicines = await _medicineService.GetAllAsync(
            cancellationToken);

        return Ok(medicines);
    }

    /// <summary>
    /// Returns a medicine-catalog entry or a not-found response.
    /// </summary>
    [Authorize(Policy = ApplicationPolicies.ViewMedicines)]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<MedicineDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var medicine = await _medicineService.GetByIdAsync(
            id,
            cancellationToken);

        return medicine is null
            ? NotFound()
            : Ok(medicine);
    }

    /// <summary>
    /// Delegates authorized catalog creation to the application service; no branch inventory is added.
    /// </summary>
    [Authorize(Policy = ApplicationPolicies.ManageMedicines)]
    [HttpPost]
    public async Task<ActionResult<MedicineDto>> Create(
        [FromBody] CreateMedicineRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var medicine = await _medicineService.CreateAsync(
                request,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { id = medicine.Id },
                medicine);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Delegates authorized catalog edits without rewriting original invoice snapshots.
    /// </summary>
    [Authorize(Policy = ApplicationPolicies.ManageMedicines)]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<MedicineDto>> Update(
        int id,
        [FromBody] UpdateMedicineRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var medicine = await _medicineService.UpdateAsync(
                id,
                request,
                cancellationToken);

            return medicine is null
                ? NotFound()
                : Ok(medicine);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Soft-deactivates a catalog medicine rather than deleting stock and sale references.
    /// </summary>
    [Authorize(Policy = ApplicationPolicies.ManageMedicines)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(
        int id,
        CancellationToken cancellationToken)
    {
        var success = await _medicineService.DeactivateAsync(
            id,
            cancellationToken);

        return success
            ? NoContent()
            : NotFound();
    }
}

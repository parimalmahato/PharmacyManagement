using Microsoft.AspNetCore.Mvc;
using Pharmacy.Application.Purchasing;
using Pharmacy.Domain.Entities.Purchasing;
using Microsoft.AspNetCore.Authorization;
using Pharmacy.Application.Authorization;
namespace Pharmacy.Api.Controllers;
/// <summary>
/// Exposes shared supplier catalog operations for authorized purchasing staff.
/// </summary>
[Authorize(Policy = ApplicationPolicies.ManagePurchasing)]
[ApiController]
[Route("api/suppliers")]
public sealed class SuppliersController(
    ISupplierRepository repository) : ControllerBase
{
    /// <summary>
    /// Returns shared supplier profiles used by purchasing.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<SupplierDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var suppliers = await repository.GetAllAsync(cancellationToken);

        return Ok(suppliers.Select(x => new SupplierDto(
            x.Id, x.Name, x.Phone, x.Email, x.Address, x.IsActive)));
    }

    /// <summary>
    /// Returns the requested supplier profile or a not-found response.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<SupplierDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var supplier = await repository.GetByIdAsync(id, cancellationToken);

        if (supplier is null)
            return NotFound();

        return Ok(new SupplierDto(
            supplier.Id,
            supplier.Name,
            supplier.Phone,
            supplier.Email,
            supplier.Address,
            supplier.IsActive));
    }

    /// <summary>
    /// Validates and saves a shared supplier profile without creating a purchase invoice.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SupplierDto>> Create(
        CreateSupplierRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var supplier = new Supplier(
                request.Name,
                request.Phone,
                request.Email,
                request.Address);

            await repository.AddAsync(supplier, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);

            var response = new SupplierDto(
                supplier.Id,
                supplier.Name,
                supplier.Phone,
                supplier.Email,
                supplier.Address,
                supplier.IsActive);

            return CreatedAtAction(
                nameof(GetById),
                new { id = supplier.Id },
                response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
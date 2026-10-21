
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Application.Customers;
using Microsoft.AspNetCore.Authorization;
using Pharmacy.Application.Authorization;
namespace Pharmacy.Api.Controllers;

/// <summary>
/// Exposes branch customer viewing and authorized profile maintenance while preserving invoice references.
/// </summary>
[ApiController]
[Route("api/customers")]
public sealed class CustomersController(
    ICustomerRepository customerRepository) : ControllerBase
{
    /// <summary>
    /// Returns customer profiles for the current branch.
    /// </summary>
    [Authorize(Policy = ApplicationPolicies.ViewCustomers)]
    [HttpGet]
    public async Task<ActionResult<List<CustomerDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var customers = await customerRepository.GetAllAsync(
            cancellationToken);

        return Ok(customers);
    }
    /// <summary>
    /// Returns a branch-visible customer or a not-found response.
    /// </summary>
    [Authorize(Policy = ApplicationPolicies.ViewCustomers)]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<CustomerDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var customer = await customerRepository.GetByIdAsync(
            id,
            cancellationToken);

        if (customer is null)
        {
            return NotFound(new
            {
                message = $"Active customer with ID {id} was not found."
            });
        }

        return Ok(customer);
    }
    /// <summary>
    /// Validates and creates a customer profile in the active branch for authorized billing staff.
    /// </summary>
    [Authorize(Policy = ApplicationPolicies.ManageCustomers)]
    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Create(
        CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var customer = await customerRepository.CreateAsync(
                request,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { id = customer.Id },
                customer);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
    /// <summary>
    /// Updates an eligible customer profile without changing historical invoice financial data.
    /// </summary>
    [Authorize(Policy = ApplicationPolicies.ManageCustomers)]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<CustomerDto>> Update(
        int id,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var customer = await customerRepository.UpdateAsync(
                id,
                request,
                cancellationToken);

            if (customer is null)
            {
                return NotFound(new
                {
                    message = $"Active customer with ID {id} was not found."
                });
            }

            return Ok(customer);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
    /// <summary>
    /// Soft-deactivates a branch customer while retaining historical invoice references.
    /// </summary>
    [Authorize(Policy = ApplicationPolicies.ManageCustomers)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(
        int id,
        CancellationToken cancellationToken)
    {
        var deactivated = await customerRepository.DeactivateAsync(
            id,
            cancellationToken);

        if (!deactivated)
        {
            return NotFound(new
            {
                message = $"Active customer with ID {id} was not found."
            });
        }

        return NoContent();
    }
}

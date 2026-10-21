using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Application.Authorization;
using Pharmacy.Application.Payments.DTOs;
using Pharmacy.Application.Returns;

namespace Pharmacy.Api.Controllers;

/// <summary>
/// Exposes original-sale returns and confirmed refunds through inspection and settlement validations in the repository.
/// </summary>
[ApiController]
[Route("api/sales/{saleId:int}")]
public sealed class SalesReturnsController(ISaleReturnRepository returns) : ControllerBase
{
    /// <summary>
    /// Returns original returnable lines, prior returns and refund history for a branch-visible sale.
    /// </summary>
    [HttpGet("returns")]
    [Authorize(Policy = ApplicationPolicies.ViewSales)]
    public async Task<ActionResult<SaleReturnsDto>> Get(int saleId, CancellationToken cancellationToken)
    {
        if (saleId <= 0) return BadRequest(new { message = "Invalid sale ID." });
        var result = await returns.GetAsync(saleId, cancellationToken);
        return result is null ? NotFound(new { message = "Sale not found." }) : Ok(result);
    }

    /// <summary>
    /// Records a validated return with explicit disposition and optional approved restocking and confirmed payout.
    /// </summary>
    [HttpPost("returns")]
    [Authorize(Policy = ApplicationPolicies.CreateSales)]
    public Task<ActionResult<ReturnDto>> Create(int saleId, CreateReturnRequest request, CancellationToken cancellationToken) =>
        Execute(() => returns.CreateAsync(saleId, request, cancellationToken));

    /// <summary>
    /// Records an actual payout within refund liability; it does not trigger a bank transfer.
    /// </summary>
    [HttpPost("refunds")]
    [Authorize(Policy = ApplicationPolicies.CreateSales)]
    public Task<ActionResult<RefundDto>> Refund(int saleId, RefundRequest request, CancellationToken cancellationToken) =>
        Execute(() => returns.RefundAsync(saleId, request, cancellationToken));

    private async Task<ActionResult<T>> Execute<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}

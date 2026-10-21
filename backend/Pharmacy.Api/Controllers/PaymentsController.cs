using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Application.Authorization;
using Pharmacy.Application.Payments;
using Pharmacy.Application.Payments.DTOs;

namespace Pharmacy.Api.Controllers;

/// <summary>
/// Exposes invoice receipt history and authorized recording of actual receipts rather than initiating payment transfers.
/// </summary>
[ApiController]
[Route("api/sales/{saleId:int}/payments")]
public sealed class PaymentsController(IPaymentRepository payments) : ControllerBase
{
    /// <summary>
    /// Returns the invoice payment ledger and current balance/refund summary.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = ApplicationPolicies.ViewSales)]
    public async Task<ActionResult<SalePaymentsDto>> Get(int saleId, CancellationToken cancellationToken)
    {
        if (saleId <= 0) return BadRequest(new { message = "Invalid sale ID." });
        var result = await payments.GetBySaleAsync(saleId, cancellationToken);
        return result is null ? NotFound(new { message = "Sale not found." }) : Ok(result);
    }

    /// <summary>
    /// Records a confirmed receipt with a retry key after role and settlement validation.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = ApplicationPolicies.CreateSales)]
    public async Task<ActionResult<PaymentDto>> Record(int saleId, RecordPaymentRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            // Always return the same receipt for an identical retry.
            return Ok(await payments.RecordAsync(saleId, request, cancellationToken));
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}

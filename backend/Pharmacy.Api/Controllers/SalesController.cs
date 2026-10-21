
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Sales;
using Pharmacy.Application.Settings;
using Pharmacy.Application.Authorization;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Api.Controllers;

/// <summary>
/// Exposes branch checkout, invoice retrieval, unsigned UPI requests and authorized cancellation with role-specific access.
/// </summary>
[ApiController]
[Route("api/sales")]
public sealed class SalesController : ControllerBase
{
    /// <summary>
    /// Builds an unsigned branch-payee UPI request for an outstanding invoice without recording a receipt.
    /// </summary>
    [Authorize(Policy = ApplicationPolicies.ViewSales)]
    [HttpGet("{id:int}/upi-request")]
    public async Task<ActionResult<Pharmacy.Application.Payments.UpiPaymentRequestDto>> UpiRequest(int id, [FromQuery] decimal? amount,
        [FromServices] Pharmacy.Application.Payments.IUpiPaymentRequestService service, CancellationToken cancellationToken)
    {
        try { return Ok(await service.GetAsync(id, amount, cancellationToken)); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
    /// <summary>
    /// Requires an authorized cancellation role and delegates custody, inspection, adjustment and retry checks to the transactional sale repository.
    /// </summary>
    [Authorize(Roles = "Admin,Pharmacist")]
    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<SaleDto>> Cancel(int id, CancelSaleRequest request, CancellationToken cancellationToken)
    {
        var actor = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(actor)) return Unauthorized();
        try { return Ok(await _saleRepository.CancelAsync(id, request, actor, cancellationToken)); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
    private readonly ISaleRepository _saleRepository;
    private readonly PharmacyDbContext _dbContext;

    public SalesController(
        ISaleRepository saleRepository,
        PharmacyDbContext dbContext)
    {
        _saleRepository = saleRepository;
        _dbContext = dbContext;
    }

    // GET: api/sales
    /// <summary>
    /// Returns the branch billing configuration needed to display checkout tax behavior.
    /// </summary>
    [Authorize(Policy = ApplicationPolicies.ViewSales)]
    [HttpGet("billing-configuration")]
    public async Task<IActionResult> BillingConfiguration(CancellationToken cancellationToken)
    {
        var configuration = await _dbContext.PharmacySettings.AsNoTracking()
            .Select(x => new { x.GstEnabled }).SingleOrDefaultAsync(cancellationToken);
        return Ok(new { gstEnabled = configuration?.GstEnabled ?? true });
    }

    // Retrieve sales history
    /// <summary>
    /// Returns current-branch invoice history and settlement status.
    /// </summary>
    [Authorize(Policy = ApplicationPolicies.ViewSales)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SaleDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var sales = await _saleRepository.GetAllAsync(
            cancellationToken);

        return Ok(sales);
    }

    // GET: api/sales/1
    // Retrieve a sale by ID
    /// <summary>
    /// Returns one branch-visible invoice and its current settlement information.
    /// </summary>
    [Authorize(Policy = ApplicationPolicies.ViewSales)]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<SaleDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return BadRequest(new
            {
                message = "Invalid sale ID."
            });
        }

        var sale = await _saleRepository.GetByIdAsync(
            id,
            cancellationToken);

        if (sale is null)
        {
            return NotFound(new
            {
                message = "Sale not found."
            });
        }

        return Ok(sale);
    }

    // GET: api/sales/1/invoice
    // Retrieve sale and pharmacy details for printing
    /// <summary>
    /// Assembles a printable invoice view from preserved sale information, branch profile and payment/return history.
    /// </summary>
    [Authorize(Policy = ApplicationPolicies.ViewSales)]
    [HttpGet("{id:int}/invoice")]
    public async Task<ActionResult<SaleInvoiceDto>> GetInvoice(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return BadRequest(new
            {
                message = "Invalid sale ID."
            });
        }

        // Retrieve the completed sale
        var sale = await _saleRepository.GetByIdAsync(
            id,
            cancellationToken);

        if (sale is null)
        {
            return NotFound(new
            {
                message = "Sale not found."
            });
        }

        // Retrieve pharmacy configuration
        var settings = await _dbContext.PharmacySettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is null)
        {
            return Conflict(new
            {
                message =
                    "Pharmacy settings are not configured. " +
                    "Please configure pharmacy settings " +
                    "before generating an invoice."
            });
        }

        // Map pharmacy entity to existing DTO
        var pharmacyDto = SettingsManagement.ToDto(settings);

        // Combine sale and pharmacy details
        var invoice = new SaleInvoiceDto(
            sale,
            pharmacyDto
        );

        return Ok(invoice);
    }

    // POST: api/sales
    // Create sale with existing FEFO inventory logic
    /// <summary>
    /// Accepts authorized checkout input and delegates final batch allocation and atomic posting to the sale repository.
    /// </summary>
    [Authorize(Policy = ApplicationPolicies.CreateSales)]
    [HttpPost]
    public async Task<ActionResult<SaleDto>> Create(
        [FromBody] CreateSaleRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var sale = await _saleRepository.CreateAsync(
                request,
                cancellationToken);

            return Created(
                $"/api/sales/{sale.Id}",
                sale);
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
    }
}

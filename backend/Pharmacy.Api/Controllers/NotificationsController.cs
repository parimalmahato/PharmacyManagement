
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Pharmacy.Application.Authorization;
namespace Pharmacy.Api.Controllers;
/// <summary>
/// Exposes branch expiry alerts and their read acknowledgements without changing inventory eligibility.
/// </summary>
[Authorize(Policy = ApplicationPolicies.ManageNotifications)]
[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationRepository _repository;

    public NotificationsController(
        INotificationRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Lists current-branch notifications, optionally showing only unread messages.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<NotificationDto>>> GetAll(
        [FromQuery] bool unreadOnly = false,
        CancellationToken cancellationToken = default)
    {
        var notifications = await _repository.GetAllAsync(
            unreadOnly,
            cancellationToken);

        return Ok(notifications);
    }

    /// <summary>
    /// Returns branch notification counts for the navigation badge.
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<NotificationSummaryDto>> GetSummary(
        CancellationToken cancellationToken)
    {
        var summary = await _repository.GetSummaryAsync(
            cancellationToken);

        return Ok(summary);
    }

    /// <summary>
    /// Acknowledges one visible branch alert without resolving the physical stock issue.
    /// </summary>
    [HttpPut("{id:long}/read")]
    public async Task<IActionResult> MarkAsRead(
        long id,
        CancellationToken cancellationToken)
    {
        var updated = await _repository.MarkAsReadAsync(
            id,
            cancellationToken);

        if (!updated)
        {
            return NotFound(new
            {
                message = "Notification not found."
            });
        }

        return NoContent();
    }

    /// <summary>
    /// Acknowledges unread alerts in the active branch without altering stock eligibility.
    /// </summary>
    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead(
        CancellationToken cancellationToken)
    {
        var updatedCount = await _repository.MarkAllAsReadAsync(
            cancellationToken);

        return Ok(new
        {
            updatedCount
        });
    }
}

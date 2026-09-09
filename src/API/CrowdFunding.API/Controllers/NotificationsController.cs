using CrowdFunding.API.Contracts.Notifications;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Notifications.Application.Features.Preferences.Commands.Unsubscribe;
using CrowdFunding.Modules.Notifications.Application.Features.Preferences.Commands.UpdateNotificationPreferences;
using CrowdFunding.Modules.Notifications.Application.Features.Preferences.Queries.GetNotificationPreferences;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrowdFunding.API.Controllers;

/// <summary>
/// Exposes HTTP endpoints for user notification preferences and GDPR/CAN-SPAM unsubscribe compliance (TICKET-056).
/// </summary>
[ApiController]
[Route("api/notifications")]
[Tags("Notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly IQueryDispatcher _queryDispatcher;
    private readonly ICurrentUser _currentUser;

    public NotificationsController(
        ICommandDispatcher commandDispatcher,
        IQueryDispatcher queryDispatcher,
        ICurrentUser currentUser)
    {
        _commandDispatcher = commandDispatcher;
        _queryDispatcher = queryDispatcher;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Retrieves notification preferences for the currently authenticated user.
    /// </summary>
    /// <response code="200">Notification preferences retrieved successfully.</response>
    /// <response code="401">The user is not authenticated.</response>
    [HttpGet("preferences")]
    [Authorize]
    [ProducesResponseType(typeof(NotificationPreferencesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<NotificationPreferencesResponse>> GetPreferences(CancellationToken cancellationToken)
    {
        var result = await _queryDispatcher.QueryAsync<GetNotificationPreferencesResult>(
            new GetNotificationPreferencesQuery(_currentUser.UserId),
            cancellationToken);

        return Ok(new NotificationPreferencesResponse(
            result.UserId,
            result.CampaignUpdatesEnabled,
            result.MarketingAnnouncementsEnabled,
            result.UpdatedAtUtc));
    }

    /// <summary>
    /// Updates notification preferences for the currently authenticated user.
    /// </summary>
    /// <response code="200">Notification preferences updated successfully.</response>
    /// <response code="401">The user is not authenticated.</response>
    [HttpPut("preferences")]
    [Authorize]
    [ProducesResponseType(typeof(NotificationPreferencesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<NotificationPreferencesResponse>> UpdatePreferences(
        [FromBody] UpdateNotificationPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _commandDispatcher.SendAsync<UpdateNotificationPreferencesResult>(
            new UpdateNotificationPreferencesCommand(
                _currentUser.UserId,
                request.CampaignUpdatesEnabled,
                request.MarketingAnnouncementsEnabled),
            cancellationToken);

        return Ok(new NotificationPreferencesResponse(
            result.UserId,
            result.CampaignUpdatesEnabled,
            result.MarketingAnnouncementsEnabled,
            result.UpdatedAtUtc));
    }

    /// <summary>
    /// One-click unsubscribe endpoint enforcing CAN-SPAM and RFC 8058 compliance.
    /// Accessible by authenticated users or via explicit userId in request.
    /// </summary>
    /// <response code="200">Successfully unsubscribed from non-transactional notifications.</response>
    /// <response code="400">UserId could not be determined.</response>
    [HttpPost("unsubscribe")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UnsubscribeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UnsubscribeResponse>> Unsubscribe(
        [FromBody] UnsubscribeRequest? request,
        CancellationToken cancellationToken)
    {
        var targetUserId = request?.UserId ?? (_currentUser.IsAuthenticated ? _currentUser.UserId : Guid.Empty);

        if (targetUserId == Guid.Empty)
        {
            return BadRequest("A valid UserId must be provided to unsubscribe.");
        }

        var result = await _commandDispatcher.SendAsync<UnsubscribeResult>(
            new UnsubscribeCommand(targetUserId),
            cancellationToken);

        return Ok(new UnsubscribeResponse(result.UserId, result.Unsubscribed));
    }
}

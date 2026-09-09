using CrowdFunding.API.Contracts.CampaignUpdates;
using CrowdFunding.API.Validation;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.CampaignUpdates.Application.Features.WebhookSubscriptions.Commands.DeleteWebhookSubscription;
using CrowdFunding.Modules.CampaignUpdates.Application.Features.WebhookSubscriptions.Commands.RegisterWebhookSubscription;
using CrowdFunding.Modules.CampaignUpdates.Application.Features.WebhookSubscriptions.Queries.GetWebhookSubscriptionById;
using CrowdFunding.Modules.CampaignUpdates.Application.Features.WebhookSubscriptions.Queries.ListWebhookSubscriptionsByCampaign;
using FluentValidation;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrowdFunding.API.Controllers;

/// <summary>
/// Lets a campaign's creator register outbound webhook subscriptions for pledge activity — their
/// CRM, Zapier, or accounting system. See TICKET-035.
/// </summary>
[ApiController]
[Route("api/campaigns/{campaignId:guid}/webhook-subscriptions")]
[Tags("CampaignUpdates")]
[Authorize]
public sealed class CampaignWebhookSubscriptionsController : ControllerBase
{
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly IQueryDispatcher _queryDispatcher;
    private readonly IValidator<RegisterWebhookSubscriptionCommand> _validator;
    private readonly IMapper _mapper;

    public CampaignWebhookSubscriptionsController(
        ICommandDispatcher commandDispatcher,
        IQueryDispatcher queryDispatcher,
        IValidator<RegisterWebhookSubscriptionCommand> validator,
        IMapper mapper)
    {
        _commandDispatcher = commandDispatcher;
        _queryDispatcher = queryDispatcher;
        _validator = validator;
        _mapper = mapper;
    }

    /// <summary>
    /// Registers a webhook subscription for a campaign. Only the campaign's owner may do this.
    /// </summary>
    /// <response code="201">Subscription registered — the response's <c>secretKey</c> is shown
    /// only this once.</response>
    /// <response code="400">Validation failed, or the target URL is not a safe public HTTPS
    /// address (private/loopback/link-local addresses are rejected).</response>
    /// <response code="403">The current user does not own this campaign.</response>
    /// <response code="404">Campaign not found.</response>
    [HttpPost]
    [ProducesResponseType(typeof(RegisterWebhookSubscriptionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RegisterWebhookSubscriptionResponse>> Register(
        [FromRoute] Guid campaignId,
        [FromBody] RegisterWebhookSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RegisterWebhookSubscriptionCommand(campaignId, request.TargetUrl);
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        var result = await _commandDispatcher.SendAsync<RegisterWebhookSubscriptionResult>(command, cancellationToken);
        var response = _mapper.Map<RegisterWebhookSubscriptionResponse>(result);

        // RFC 9110 §10.2.2: the Location header must resolve — generated from the route
        // collection via the GetById action below rather than a hand-built string, so it can
        // never drift out of sync with the actual GET route (TICKET-047).
        return CreatedAtAction(nameof(GetById), new { campaignId, subscriptionId = response.SubscriptionId }, response);
    }

    /// <summary>
    /// Lists every webhook subscription registered for a campaign. Only the campaign's owner may
    /// view this.
    /// </summary>
    /// <response code="200">Returns the campaign's webhook subscriptions.</response>
    /// <response code="403">The current user does not own this campaign.</response>
    /// <response code="404">Campaign not found.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ListWebhookSubscriptionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ListWebhookSubscriptionsResponse>> List(
        [FromRoute] Guid campaignId,
        CancellationToken cancellationToken)
    {
        var result = await _queryDispatcher.QueryAsync<ListWebhookSubscriptionsByCampaignResult>(
            new ListWebhookSubscriptionsByCampaignQuery(campaignId), cancellationToken);

        return Ok(_mapper.Map<ListWebhookSubscriptionsResponse>(result));
    }

    /// <summary>
    /// Retrieves a single webhook subscription by its unique identifier. Only the campaign's
    /// owner may view this.
    /// </summary>
    /// <response code="200">Returns the webhook subscription.</response>
    /// <response code="403">The current user does not own this campaign.</response>
    /// <response code="404">Subscription not found for the given campaign.</response>
    [HttpGet("{subscriptionId:guid}")]
    [ProducesResponseType(typeof(WebhookSubscriptionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WebhookSubscriptionResponse>> GetById(
        [FromRoute] Guid campaignId,
        [FromRoute] Guid subscriptionId,
        CancellationToken cancellationToken)
    {
        var result = await _queryDispatcher.QueryAsync<GetWebhookSubscriptionByIdResult>(
            new GetWebhookSubscriptionByIdQuery(campaignId, subscriptionId), cancellationToken);

        return Ok(_mapper.Map<WebhookSubscriptionResponse>(result));
    }

    /// <summary>
    /// Deactivates a webhook subscription so it no longer receives deliveries. Only the
    /// campaign's owner may do this.
    /// </summary>
    /// <response code="204">Subscription deactivated.</response>
    /// <response code="403">The current user does not own this campaign.</response>
    /// <response code="404">Subscription not found for the given campaign.</response>
    [HttpDelete("{subscriptionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromRoute] Guid campaignId,
        [FromRoute] Guid subscriptionId,
        CancellationToken cancellationToken)
    {
        await _commandDispatcher.SendAsync<DeleteWebhookSubscriptionResult>(
            new DeleteWebhookSubscriptionCommand(campaignId, subscriptionId), cancellationToken);

        return NoContent();
    }
}

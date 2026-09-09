using CrowdFunding.API.Contracts.CampaignUpdates;
using CrowdFunding.API.Validation;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.CampaignUpdates.Application.Features.WebhookSubscriptions.Commands.RegisterWebhookSubscription;
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
    private readonly IValidator<RegisterWebhookSubscriptionCommand> _validator;
    private readonly IMapper _mapper;

    public CampaignWebhookSubscriptionsController(
        ICommandDispatcher commandDispatcher,
        IValidator<RegisterWebhookSubscriptionCommand> validator,
        IMapper mapper)
    {
        _commandDispatcher = commandDispatcher;
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

        return Created($"/api/campaigns/{campaignId}/webhook-subscriptions/{response.SubscriptionId}", response);
    }
}

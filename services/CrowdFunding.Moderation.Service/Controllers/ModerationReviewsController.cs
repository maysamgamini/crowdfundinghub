using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Moderation.Service.Contracts;
using CrowdFunding.Moderation.Service.Validation;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.ApproveCampaignReview;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.RejectCampaignReview;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Queries.GetCampaignReviewByCampaignId;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrowdFunding.Moderation.Service.Controllers;

/// <summary>
/// TICKET-029's extraction proof: this controller is the ONLY new code in this project. Every
/// line below it (validation, command/query dispatch, transactions, persistence, outbox
/// publication) is the exact same <c>CrowdFunding.Modules.Moderation.*</c> code the monolith
/// runs — nothing here re-implements or forks Moderation's business logic.
/// </summary>
[ApiController]
[Route("api/moderation/reviews")]
[Authorize]
public sealed class ModerationReviewsController : ControllerBase
{
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly IQueryDispatcher _queryDispatcher;
    private readonly IValidator<ApproveCampaignReviewCommand> _approveValidator;
    private readonly IValidator<RejectCampaignReviewCommand> _rejectValidator;

    public ModerationReviewsController(
        ICommandDispatcher commandDispatcher,
        IQueryDispatcher queryDispatcher,
        IValidator<ApproveCampaignReviewCommand> approveValidator,
        IValidator<RejectCampaignReviewCommand> rejectValidator)
    {
        _commandDispatcher = commandDispatcher;
        _queryDispatcher = queryDispatcher;
        _approveValidator = approveValidator;
        _rejectValidator = rejectValidator;
    }

    [HttpGet("{campaignId:guid}")]
    public async Task<ActionResult<GetCampaignReviewByCampaignIdResult>> GetByCampaignId(
        [FromRoute] Guid campaignId, CancellationToken cancellationToken)
    {
        var result = await _queryDispatcher.QueryAsync<GetCampaignReviewByCampaignIdResult>(
            new GetCampaignReviewByCampaignIdQuery(campaignId), cancellationToken);

        return Ok(result);
    }

    [HttpPost("{campaignId:guid}/approve")]
    public async Task<ActionResult<ApproveCampaignReviewResult>> Approve(
        [FromRoute] Guid campaignId, [FromBody] ReviewCampaignRequest request, CancellationToken cancellationToken)
    {
        var command = new ApproveCampaignReviewCommand(campaignId, request.Notes);
        var validationResult = await _approveValidator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        var result = await _commandDispatcher.SendAsync<ApproveCampaignReviewResult>(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{campaignId:guid}/reject")]
    public async Task<ActionResult<RejectCampaignReviewResult>> Reject(
        [FromRoute] Guid campaignId, [FromBody] ReviewCampaignRequest request, CancellationToken cancellationToken)
    {
        var command = new RejectCampaignReviewCommand(campaignId, request.Notes);
        var validationResult = await _rejectValidator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        var result = await _commandDispatcher.SendAsync<RejectCampaignReviewResult>(command, cancellationToken);
        return Ok(result);
    }
}

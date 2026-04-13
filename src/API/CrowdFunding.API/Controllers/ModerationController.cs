using CrowdFunding.API.Contracts.Moderation;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.ApproveCampaignReview;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.RejectCampaignReview;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Queries.GetCampaignReviewByCampaignId;
using FluentValidation;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrowdFunding.API.Controllers;

/// <summary>
/// Exposes HTTP endpoints for Moderation.
/// </summary>
[ApiController]
[Route("api/moderation/campaigns")]
public sealed class ModerationController : ControllerBase
{
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly IQueryDispatcher _queryDispatcher;
    private readonly IValidator<ApproveCampaignReviewCommand> _approveCampaignReviewValidator;
    private readonly IValidator<RejectCampaignReviewCommand> _rejectCampaignReviewValidator;
    private readonly IMapper _mapper;

    public ModerationController(
        ICommandDispatcher commandDispatcher,
        IQueryDispatcher queryDispatcher,
        IValidator<ApproveCampaignReviewCommand> approveCampaignReviewValidator,
        IValidator<RejectCampaignReviewCommand> rejectCampaignReviewValidator,
        IMapper mapper)
    {
        _commandDispatcher = commandDispatcher;
        _queryDispatcher = queryDispatcher;
        _approveCampaignReviewValidator = approveCampaignReviewValidator;
        _rejectCampaignReviewValidator = rejectCampaignReviewValidator;
        _mapper = mapper;
    }

    [HttpGet("{campaignId:guid}")]
    [ProducesResponseType(typeof(CampaignReviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CampaignReviewResponse>> GetByCampaignId(Guid campaignId, CancellationToken cancellationToken)
    {
        var result = await _queryDispatcher.QueryAsync<GetCampaignReviewByCampaignIdResult>(
            new GetCampaignReviewByCampaignIdQuery(campaignId),
            cancellationToken);

        return Ok(_mapper.Map<CampaignReviewResponse>(result));
    }

    [Authorize(Policy = PermissionConstants.ModerationReview)]
    [HttpPost("{campaignId:guid}/approve")]
    [ProducesResponseType(typeof(CampaignReviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CampaignReviewResponse>> Approve(
        Guid campaignId,
        [FromBody] ReviewCampaignRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ApproveCampaignReviewCommand(campaignId, request.Notes);
        var validationResult = await _approveCampaignReviewValidator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        await _commandDispatcher.SendAsync<ApproveCampaignReviewResult>(command, cancellationToken);

        var review = await _queryDispatcher.QueryAsync<GetCampaignReviewByCampaignIdResult>(
            new GetCampaignReviewByCampaignIdQuery(campaignId),
            cancellationToken);

        return Ok(_mapper.Map<CampaignReviewResponse>(review));
    }

    [Authorize(Policy = PermissionConstants.ModerationReview)]
    [HttpPost("{campaignId:guid}/reject")]
    [ProducesResponseType(typeof(CampaignReviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CampaignReviewResponse>> Reject(
        Guid campaignId,
        [FromBody] ReviewCampaignRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RejectCampaignReviewCommand(campaignId, request.Notes);
        var validationResult = await _rejectCampaignReviewValidator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        await _commandDispatcher.SendAsync<RejectCampaignReviewResult>(command, cancellationToken);

        var review = await _queryDispatcher.QueryAsync<GetCampaignReviewByCampaignIdResult>(
            new GetCampaignReviewByCampaignIdQuery(campaignId),
            cancellationToken);

        return Ok(_mapper.Map<CampaignReviewResponse>(review));
    }
}

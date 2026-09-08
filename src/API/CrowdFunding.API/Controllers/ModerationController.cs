using CrowdFunding.API.Contracts.Common;
using CrowdFunding.API.Contracts.Moderation;
using CrowdFunding.API.Validation;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Pagination;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.ApproveCampaignReview;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.RejectCampaignReview;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Queries.GetCampaignReviewByCampaignId;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Queries.ListCampaignReviews;
using FluentValidation;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrowdFunding.API.Controllers;

/// <summary>
/// Exposes HTTP endpoints for Moderation: campaign compliance reviews, approvals, and rejection feedback.
/// Routed under <c>api/moderation/reviews</c> — the review is the module's own resource, distinct
/// from the campaign resource already owned by <see cref="CampaignsController"/>
/// (QA TICKET-013 Issue B).
/// </summary>
[ApiController]
[Route("api/moderation/reviews")]
[Tags("Moderation")]
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

    /// <summary>
    /// Retrieves the moderation review queue, optionally filtered by status.
    /// </summary>
    /// <param name="status">Optional status filter (Pending, Approved, Rejected).</param>
    /// <param name="pageNumber">The page number to retrieve (1-based, default: 1).</param>
    /// <param name="pageSize">The number of items per page (default: 20).</param>
    /// <param name="cancellationToken">Cancellation token for asynchronous operation.</param>
    /// <response code="200">Returns the requested page of moderation reviews.</response>
    /// <response code="401">Unauthorized if the request lacks a valid Bearer token.</response>
    /// <response code="403">Forbidden if the caller lacks the 'moderation:review' permission.</response>
    [Authorize(Policy = PermissionConstants.ModerationReview)]
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<CampaignReviewResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<CampaignReviewResponse>>> List(
        [FromQuery] string? status,
        [FromQuery] int? pageNumber,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var pageRequest = PageRequest.Create(pageNumber, pageSize);
        var result = await _queryDispatcher.QueryAsync<PagedResult<GetCampaignReviewByCampaignIdResult>>(
            new ListCampaignReviewsQuery(pageRequest, status),
            cancellationToken);

        var items = result.Items.Select(x => _mapper.Map<CampaignReviewResponse>(x)).ToArray();
        var response = new PagedResponse<CampaignReviewResponse>(
            items,
            result.PageNumber,
            result.PageSize,
            result.TotalCount,
            result.TotalPages);

        return Ok(response);
    }

    /// <summary>
    /// Retrieves moderation review details for a specific campaign.
    /// </summary>
    /// <param name="campaignId">The unique identifier of the campaign.</param>
    /// <param name="cancellationToken">Cancellation token for asynchronous operation.</param>
    /// <response code="200">Returns moderation review details including review status and notes.</response>
    /// <response code="401">Unauthorized if the request lacks a valid Bearer token.</response>
    /// <response code="403">Forbidden if the caller lacks the 'moderation:review' permission.</response>
    /// <response code="404">Campaign review not found.</response>
    [Authorize(Policy = PermissionConstants.ModerationReview)]
    [HttpGet("{campaignId:guid}")]
    [ProducesResponseType(typeof(CampaignReviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CampaignReviewResponse>> GetByCampaignId([FromRoute] Guid campaignId, CancellationToken cancellationToken)
    {
        var result = await _queryDispatcher.QueryAsync<GetCampaignReviewByCampaignIdResult>(
            new GetCampaignReviewByCampaignIdQuery(campaignId),
            cancellationToken);

        return Ok(_mapper.Map<CampaignReviewResponse>(result));
    }

    /// <summary>
    /// Approves a campaign review, enabling the creator to publish the campaign.
    /// </summary>
    /// <param name="campaignId">The unique identifier of the campaign to approve.</param>
    /// <param name="request">The approval request payload containing optional review notes.</param>
    /// <param name="cancellationToken">Cancellation token for asynchronous operation.</param>
    /// <response code="200">Campaign review approved successfully.</response>
    /// <response code="400">Review cannot be approved or validation errors occurred.</response>
    /// <response code="401">Unauthorized if the request lacks a valid Bearer token.</response>
    /// <response code="403">Forbidden if the caller lacks the 'moderation:review' permission.</response>
    /// <response code="404">Campaign review not found.</response>
    [Authorize(Policy = PermissionConstants.ModerationReview)]
    [HttpPost("{campaignId:guid}/approve")]
    [ProducesResponseType(typeof(CampaignReviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CampaignReviewResponse>> Approve(
        [FromRoute] Guid campaignId,
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

    /// <summary>
    /// Rejects a campaign review, providing feedback notes explaining required changes.
    /// </summary>
    /// <param name="campaignId">The unique identifier of the campaign to reject.</param>
    /// <param name="request">The rejection request payload containing review notes.</param>
    /// <param name="cancellationToken">Cancellation token for asynchronous operation.</param>
    /// <response code="200">Campaign review rejected.</response>
    /// <response code="400">Review cannot be rejected or validation errors occurred.</response>
    /// <response code="401">Unauthorized if the request lacks a valid Bearer token.</response>
    /// <response code="403">Forbidden if the caller lacks the 'moderation:review' permission.</response>
    /// <response code="404">Campaign review not found.</response>
    [Authorize(Policy = PermissionConstants.ModerationReview)]
    [HttpPost("{campaignId:guid}/reject")]
    [ProducesResponseType(typeof(CampaignReviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CampaignReviewResponse>> Reject(
        [FromRoute] Guid campaignId,
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

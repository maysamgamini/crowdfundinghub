using CrowdFunding.API.Contracts.Common;
using CrowdFunding.API.Contracts.Moderation;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Pagination;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.ApproveCampaignReview;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.RejectCampaignReview;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Queries.GetCampaignReviewByCampaignId;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Queries.ListCampaignReviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrowdFunding.API.Controllers;

/// <summary>
/// Exposes HTTP endpoints for Moderation: campaign compliance reviews, approvals, and rejection feedback.
/// Routed under <c>api/moderation/reviews</c> — the review is the module's own resource, distinct
/// from the campaign resource owned by <c>CampaignEndpoints</c> (QA TICKET-013 Issue B).
/// </summary>
/// <remarks>
/// <b>Kept as an MVC controller on purpose, for comparison.</b> Every other endpoint of this API is a
/// Minimal API (see <c>Endpoints/</c>). This controller has the same shape as
/// <c>Endpoints/CampaignEndpoints</c> (a paged list, a get-by-id, POST actions behind permission
/// policies) so the two styles can be read side by side: attributes versus fluent endpoint
/// metadata, constructor injection versus handler parameters, <c>ActionResult</c> versus
/// <c>TypedResults</c>. Both run through the same command pipeline, exception handler, OpenAPI
/// document and rate limiter. See docs/minimal-apis-vs-controllers.md.
/// </remarks>
[ApiController]
[Route("api/moderation/reviews")]
[Tags("Moderation")]
public sealed class ModerationController : ControllerBase
{
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly IQueryDispatcher _queryDispatcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModerationController"/> class.
    /// </summary>
    /// <param name="commandDispatcher">The command dispatcher.</param>
    /// <param name="queryDispatcher">The query dispatcher.</param>
    public ModerationController(ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
    {
        _commandDispatcher = commandDispatcher;
        _queryDispatcher = queryDispatcher;
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

        return Ok(PagedResponse.From(result, ToResponse));
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

        return Ok(ToResponse(result));
    }

    /// <summary>
    /// Approves a campaign review, enabling the creator to publish the campaign.
    /// </summary>
    /// <param name="campaignId">The unique identifier of the campaign to approve.</param>
    /// <param name="request">The approval request payload containing optional review notes.</param>
    /// <param name="cancellationToken">Cancellation token for asynchronous operation.</param>
    /// <response code="200">Campaign review approved successfully.</response>
    /// <response code="400">Validation errors occurred.</response>
    /// <response code="409">The review is no longer pending.</response>
    /// <response code="401">Unauthorized if the request lacks a valid Bearer token.</response>
    /// <response code="403">Forbidden if the caller lacks the 'moderation:review' permission.</response>
    /// <response code="404">Campaign review not found.</response>
    [Authorize(Policy = PermissionConstants.ModerationReview)]
    [HttpPost("{campaignId:guid}/approve")]
    [ProducesResponseType(typeof(CampaignReviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CampaignReviewResponse>> Approve(
        [FromRoute] Guid campaignId,
        [FromBody] ReviewCampaignRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ApproveCampaignReviewCommand(campaignId, request.Notes);

        await _commandDispatcher.SendAsync<ApproveCampaignReviewResult>(command, cancellationToken);

        var review = await _queryDispatcher.QueryAsync<GetCampaignReviewByCampaignIdResult>(
            new GetCampaignReviewByCampaignIdQuery(campaignId),
            cancellationToken);

        return Ok(ToResponse(review));
    }

    /// <summary>
    /// Rejects a campaign review, providing feedback notes explaining required changes.
    /// </summary>
    /// <param name="campaignId">The unique identifier of the campaign to reject.</param>
    /// <param name="request">The rejection request payload containing review notes.</param>
    /// <param name="cancellationToken">Cancellation token for asynchronous operation.</param>
    /// <response code="200">Campaign review rejected.</response>
    /// <response code="400">Validation errors occurred.</response>
    /// <response code="409">The review is no longer pending.</response>
    /// <response code="401">Unauthorized if the request lacks a valid Bearer token.</response>
    /// <response code="403">Forbidden if the caller lacks the 'moderation:review' permission.</response>
    /// <response code="404">Campaign review not found.</response>
    [Authorize(Policy = PermissionConstants.ModerationReview)]
    [HttpPost("{campaignId:guid}/reject")]
    [ProducesResponseType(typeof(CampaignReviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CampaignReviewResponse>> Reject(
        [FromRoute] Guid campaignId,
        [FromBody] ReviewCampaignRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RejectCampaignReviewCommand(campaignId, request.Notes);

        await _commandDispatcher.SendAsync<RejectCampaignReviewResult>(command, cancellationToken);

        var review = await _queryDispatcher.QueryAsync<GetCampaignReviewByCampaignIdResult>(
            new GetCampaignReviewByCampaignIdQuery(campaignId),
            cancellationToken);

        return Ok(ToResponse(review));
    }

    private static CampaignReviewResponse ToResponse(GetCampaignReviewByCampaignIdResult review)
        => new(review.CampaignId, review.Status, review.ModeratorId, review.Notes, review.CreatedAtUtc, review.ReviewedAtUtc);
}

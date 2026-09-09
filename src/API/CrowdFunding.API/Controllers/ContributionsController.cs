using CrowdFunding.API.Contracts.Common;
using CrowdFunding.API.Contracts.Contributions;
using CrowdFunding.API.RateLimiting;
using CrowdFunding.API.Validation;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Pagination;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.ConfirmContributionPayment;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.FailContributionPayment;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.MakeContribution;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Queries.GetContributionById;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Queries.ListContributionsByCampaign;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using FluentValidation;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CrowdFunding.API.Controllers;

/// <summary>
/// Exposes HTTP endpoints for Contributions: creating pledges, listing contributions, and handling payment confirmations and failures.
/// </summary>
[ApiController]
[Route("api/campaigns/{campaignId:guid}/[controller]")]
[Tags("Contributions")]
public sealed class ContributionsController : ControllerBase
{
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly IQueryDispatcher _queryDispatcher;
    private readonly IMapper _mapper;
    private readonly IValidator<ConfirmContributionPaymentCommand> _confirmPaymentValidator;
    private readonly IValidator<FailContributionPaymentCommand> _failPaymentValidator;
    private readonly IValidator<MakeContributionCommand> _validator;

    public ContributionsController(
        ICommandDispatcher commandDispatcher,
        IQueryDispatcher queryDispatcher,
        IMapper mapper,
        IValidator<ConfirmContributionPaymentCommand> confirmPaymentValidator,
        IValidator<FailContributionPaymentCommand> failPaymentValidator,
        IValidator<MakeContributionCommand> validator)
    {
        _commandDispatcher = commandDispatcher;
        _queryDispatcher = queryDispatcher;
        _mapper = mapper;
        _confirmPaymentValidator = confirmPaymentValidator;
        _failPaymentValidator = failPaymentValidator;
        _validator = validator;
    }

    /// <summary>
    /// Retrieves a paginated list of contributions for a specific campaign.
    /// </summary>
    /// <param name="campaignId">The unique identifier of the campaign.</param>
    /// <param name="pageNumber">The page number to retrieve (1-based, default: 1).</param>
    /// <param name="pageSize">The number of items per page (default: 10).</param>
    /// <param name="contributorId">Optional filter for contributions made by a specific contributor.</param>
    /// <param name="currency">Optional currency filter (e.g. USD, EUR).</param>
    /// <param name="status">Optional contribution status filter (Pending, Succeeded, Failed).</param>
    /// <param name="cancellationToken">Cancellation token for asynchronous operation.</param>
    /// <response code="200">Returns the requested page of contributions.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ListContributionsResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ListContributionsResponse>>> ListByCampaign(
        [FromRoute] Guid campaignId,
        [FromQuery] int? pageNumber,
        [FromQuery] int? pageSize,
        [FromQuery] Guid? contributorId,
        [FromQuery] string? currency,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var pageRequest = PageRequest.Create(pageNumber, pageSize);
        var filter = new ListContributionsByCampaignFilter(contributorId, currency, status);
        var result = await _queryDispatcher.QueryAsync<PagedResult<ListContributionsByCampaignResult>>(
            new ListContributionsByCampaignQuery(campaignId, pageRequest, filter),
            cancellationToken);

        var items = result.Items.Select(x => _mapper.Map<ListContributionsResponse>(x)).ToArray();
        var response = new PagedResponse<ListContributionsResponse>(
            items,
            result.PageNumber,
            result.PageSize,
            result.TotalCount,
            result.TotalPages);

        return Ok(response);
    }

    /// <summary>
    /// Pledges a new financial contribution to an active campaign.
    /// </summary>
    /// <param name="campaignId">The unique identifier of the target campaign.</param>
    /// <param name="request">The contribution request containing amount and currency.</param>
    /// <param name="cancellationToken">Cancellation token for asynchronous operation.</param>
    /// <response code="201">Contribution created successfully in Pending status.</response>
    /// <response code="400">Invalid contribution amount, currency mismatch, or validation errors.</response>
    /// <response code="401">Unauthorized if the request lacks a valid Bearer token.</response>
    /// <response code="403">Forbidden if the caller lacks the 'campaigns:contribute' permission.</response>
    /// <response code="404">Target campaign not found or not active.</response>
    [Authorize(Policy = PermissionConstants.CampaignsContribute)]
    [EnableRateLimiting(RateLimitingConfiguration.PaymentPolicy)]
    [HttpPost]
    [ProducesResponseType(typeof(MakeContributionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MakeContributionResponse>> Create(
        [FromRoute] Guid campaignId,
        [FromBody] MakeContributionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new MakeContributionCommand(campaignId, request.Amount, request.Currency, request.RewardTierReservationId);
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        var result = await _commandDispatcher.SendAsync<MakeContributionResult>(command, cancellationToken);
        var response = _mapper.Map<MakeContributionResponse>(result);

        // Location must point at the created resource itself (RFC 9110 §10.3.2), not the
        // collection it lives in — a client following it previously landed back on the
        // paginated list instead of the contribution just created (QA TICKET-006).
        return CreatedAtAction(nameof(GetById), new { campaignId, contributionId = result.ContributionId }, response);
    }

    /// <summary>
    /// Retrieves a single contribution by its unique identifier — the resource clients need to
    /// poll payment status (Pending -&gt; Succeeded/Failed) after creating a pledge.
    /// </summary>
    /// <param name="campaignId">The unique identifier of the campaign.</param>
    /// <param name="contributionId">The unique identifier of the contribution.</param>
    /// <param name="cancellationToken">Cancellation token for asynchronous operation.</param>
    /// <response code="200">Returns the contribution details.</response>
    /// <response code="404">Contribution not found for the given campaign.</response>
    [HttpGet("{contributionId:guid}")]
    [ProducesResponseType(typeof(GetContributionByIdResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetContributionByIdResponse>> GetById(
        [FromRoute] Guid campaignId,
        [FromRoute] Guid contributionId,
        CancellationToken cancellationToken)
    {
        var result = await _queryDispatcher.QueryAsync<GetContributionByIdResult>(
            new GetContributionByIdQuery(campaignId, contributionId),
            cancellationToken);

        return Ok(_mapper.Map<GetContributionByIdResponse>(result));
    }

    /// <summary>
    /// Confirms successful payment processing for a pending contribution.
    /// </summary>
    /// <param name="campaignId">The unique identifier of the campaign.</param>
    /// <param name="contributionId">The unique identifier of the contribution to confirm.</param>
    /// <param name="request">The confirmation payload containing external payment gateway reference.</param>
    /// <param name="cancellationToken">Cancellation token for asynchronous operation.</param>
    /// <response code="200">Payment confirmed successfully.</response>
    /// <response code="400">Payment confirmation failed or invalid state transition.</response>
    /// <response code="401">Unauthorized if the request lacks a valid Bearer token.</response>
    /// <response code="403">Forbidden if the caller lacks the 'contributions:payments:manage' permission.</response>
    /// <response code="404">Contribution or campaign not found.</response>
    /// <response code="409">Contribution was concurrently modified by another request.</response>
    [Authorize(Policy = PermissionConstants.ContributionsPaymentsManage)]
    [HttpPost("{contributionId:guid}/confirm-payment")]
    [ProducesResponseType(typeof(ConfirmContributionPaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ConfirmContributionPaymentResponse>> ConfirmPayment(
        [FromRoute] Guid campaignId,
        [FromRoute] Guid contributionId,
        [FromBody] ConfirmContributionPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ConfirmContributionPaymentCommand(campaignId, contributionId, request.PaymentReference);
        var validationResult = await _confirmPaymentValidator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        var result = await _commandDispatcher.SendAsync<ConfirmContributionPaymentResult>(command, cancellationToken);
        return Ok(_mapper.Map<ConfirmContributionPaymentResponse>(result));
    }

    /// <summary>
    /// Marks a pending contribution as failed due to payment processing issues.
    /// </summary>
    /// <param name="campaignId">The unique identifier of the campaign.</param>
    /// <param name="contributionId">The unique identifier of the contribution to mark as failed.</param>
    /// <param name="request">The failure details payload containing the reason for failure.</param>
    /// <param name="cancellationToken">Cancellation token for asynchronous operation.</param>
    /// <response code="200">Contribution marked as failed.</response>
    /// <response code="400">Validation error or invalid state transition.</response>
    /// <response code="401">Unauthorized if the request lacks a valid Bearer token.</response>
    /// <response code="403">Forbidden if the caller lacks the 'contributions:payments:manage' permission.</response>
    /// <response code="404">Contribution or campaign not found.</response>
    /// <response code="409">Contribution was concurrently modified by another request.</response>
    [Authorize(Policy = PermissionConstants.ContributionsPaymentsManage)]
    [HttpPost("{contributionId:guid}/fail-payment")]
    [ProducesResponseType(typeof(FailContributionPaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FailContributionPaymentResponse>> FailPayment(
        [FromRoute] Guid campaignId,
        [FromRoute] Guid contributionId,
        [FromBody] FailContributionPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var command = new FailContributionPaymentCommand(campaignId, contributionId, request.FailureReason);
        var validationResult = await _failPaymentValidator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        var result = await _commandDispatcher.SendAsync<FailContributionPaymentResult>(command, cancellationToken);
        return Ok(_mapper.Map<FailContributionPaymentResponse>(result));
    }
}

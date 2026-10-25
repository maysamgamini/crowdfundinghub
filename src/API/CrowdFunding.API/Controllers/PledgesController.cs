using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.ConfirmPledgeSetup;
using CrowdFunding.API.Contracts.Common;
using CrowdFunding.API.Contracts.Pledges;
using CrowdFunding.API.RateLimiting;
using CrowdFunding.API.Validation;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Pagination;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.ConfirmPledgePayment;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.FailPledgePayment;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.MakePledge;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Queries.GetPledgeById;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Queries.ListPledgesByCampaign;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using FluentValidation;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CrowdFunding.API.Controllers;

/// <summary>
/// Exposes HTTP endpoints for Pledges: creating pledges, listing pledges, and handling payment confirmations and failures.
/// </summary>
[ApiController]
[Route("api/campaigns/{campaignId:guid}/[controller]")]
[Tags("Pledges")]
public sealed class PledgesController : ControllerBase
{
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly IQueryDispatcher _queryDispatcher;
    private readonly IMapper _mapper;
    private readonly IValidator<ConfirmPledgePaymentCommand> _confirmPaymentValidator;
    private readonly IValidator<ConfirmPledgeSetupCommand> _confirmSetupValidator;
    private readonly IValidator<FailPledgePaymentCommand> _failPaymentValidator;
    private readonly IValidator<MakePledgeCommand> _validator;

    public PledgesController(
        ICommandDispatcher commandDispatcher,
        IQueryDispatcher queryDispatcher,
        IMapper mapper,
        IValidator<ConfirmPledgePaymentCommand> confirmPaymentValidator,
        IValidator<ConfirmPledgeSetupCommand> confirmSetupValidator,
        IValidator<FailPledgePaymentCommand> failPaymentValidator,
        IValidator<MakePledgeCommand> validator)
    {
        _commandDispatcher = commandDispatcher;
        _queryDispatcher = queryDispatcher;
        _mapper = mapper;
        _confirmPaymentValidator = confirmPaymentValidator;
        _confirmSetupValidator = confirmSetupValidator;
        _failPaymentValidator = failPaymentValidator;
        _validator = validator;
    }

    /// <summary>
    /// Retrieves a paginated list of pledges for a specific campaign.
    /// </summary>
    /// <param name="campaignId">The unique identifier of the campaign.</param>
    /// <param name="pageNumber">The page number to retrieve (1-based, default: 1).</param>
    /// <param name="pageSize">The number of items per page (default: 10).</param>
    /// <param name="backerId">Optional filter for pledges made by a specific backer.</param>
    /// <param name="currency">Optional currency filter (e.g. USD, EUR).</param>
    /// <param name="status">Optional pledge status filter (Pending, Succeeded, Failed).</param>
    /// <param name="cancellationToken">Cancellation token for asynchronous operation.</param>
    /// <response code="200">Returns the requested page of pledges. A backer sees only their own, the campaign's owner sees all without payment references, and a payments manager sees everything.</response>
    /// <response code="401">Unauthorized if the request lacks a valid Bearer token.</response>
    [Authorize]
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ListPledgesResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResponse<ListPledgesResponse>>> ListByCampaign(
        [FromRoute] Guid campaignId,
        [FromQuery] int? pageNumber,
        [FromQuery] int? pageSize,
        [FromQuery] Guid? backerId,
        [FromQuery] string? currency,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var pageRequest = PageRequest.Create(pageNumber, pageSize);
        var filter = new ListPledgesByCampaignFilter(backerId, currency, status);
        var result = await _queryDispatcher.QueryAsync<PagedResult<ListPledgesByCampaignResult>>(
            new ListPledgesByCampaignQuery(campaignId, pageRequest, filter),
            cancellationToken);

        var items = result.Items.Select(x => _mapper.Map<ListPledgesResponse>(x)).ToArray();
        var response = new PagedResponse<ListPledgesResponse>(
            items,
            result.PageNumber,
            result.PageSize,
            result.TotalCount,
            result.TotalPages);

        return Ok(response);
    }

    /// <summary>
    /// Pledges a new financial pledge to an active campaign.
    /// </summary>
    /// <param name="campaignId">The unique identifier of the target campaign.</param>
    /// <param name="request">The pledge request containing amount and currency.</param>
    /// <param name="idempotencyKey">Optional <c>Idempotency-Key</c> header, 1 to 100 printable ASCII characters. Send the same key when retrying a request that may not have completed (a timeout, a dropped connection, a double click) and the backer gets the first pledge back instead of a second one.</param>
    /// <param name="cancellationToken">Cancellation token for asynchronous operation.</param>
    /// <response code="201">Pledge created successfully in Pending status.</response>
    /// <response code="400">Invalid pledge amount, currency mismatch, or validation errors.</response>
    /// <response code="401">Unauthorized if the request lacks a valid Bearer token.</response>
    /// <response code="200">A retry: the same backer already used this <c>Idempotency-Key</c> for this request, so the first pledge is returned and the response carries <c>Idempotent-Replayed: true</c>.</response>
    /// <response code="409">The <c>Idempotency-Key</c> was already used by this backer for a different request.</response>
    /// <response code="403">Forbidden if the caller lacks the 'campaigns:pledge' permission.</response>
    /// <response code="404">Target campaign not found or not active.</response>
    [Authorize(Policy = PermissionConstants.CampaignsPledge)]
    [EnableRateLimiting(RateLimitingConfiguration.PaymentPolicy)]
    [HttpPost]
    [ProducesResponseType(typeof(MakePledgeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(MakePledgeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MakePledgeResponse>> Create(
        [FromRoute] Guid campaignId,
        [FromBody] MakePledgeRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var command = new MakePledgeCommand(
            campaignId, request.Amount, request.Currency, request.RewardTierReservationId, idempotencyKey);
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        var result = await _commandDispatcher.SendAsync<MakePledgeResult>(command, cancellationToken);
        var response = _mapper.Map<MakePledgeResponse>(result);

        // A retry with a key already used by this backer returns the first pledge unchanged.
        if (result.IsReplay)
        {
            Response.Headers["Idempotent-Replayed"] = "true";
            return Ok(response);
        }

        // Location must point at the created resource itself (RFC 9110 §10.3.2), not the
        // collection it lives in — a client following it previously landed back on the
        // paginated list instead of the pledge just created (QA TICKET-006).
        return CreatedAtAction(nameof(GetById), new { campaignId, pledgeId = result.PledgeId }, response);
    }

    /// <summary>
    /// Retrieves a single pledge by its unique identifier — the resource clients need to
    /// poll payment status (Pending -&gt; Succeeded/Failed) after creating a pledge.
    /// </summary>
    /// <param name="campaignId">The unique identifier of the campaign.</param>
    /// <param name="pledgeId">The unique identifier of the pledge.</param>
    /// <param name="cancellationToken">Cancellation token for asynchronous operation.</param>
    /// <response code="200">Returns the pledge details. The backer who made it, the campaign's owner (without payment references) and a payments manager may read it.</response>
    /// <response code="401">Unauthorized if the request lacks a valid Bearer token.</response>
    /// <response code="404">Pledge not found for the given campaign, or not visible to the caller.</response>
    [Authorize]
    [HttpGet("{pledgeId:guid}")]
    [ProducesResponseType(typeof(GetPledgeByIdResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetPledgeByIdResponse>> GetById(
        [FromRoute] Guid campaignId,
        [FromRoute] Guid pledgeId,
        CancellationToken cancellationToken)
    {
        var result = await _queryDispatcher.QueryAsync<GetPledgeByIdResult>(
            new GetPledgeByIdQuery(campaignId, pledgeId),
            cancellationToken);

        return Ok(_mapper.Map<GetPledgeByIdResponse>(result));
    }

    /// <summary>
    /// Confirms successful payment processing for a pending pledge.
    /// </summary>
    /// <param name="campaignId">The unique identifier of the campaign.</param>
    /// <param name="pledgeId">The unique identifier of the pledge to confirm.</param>
    /// <param name="request">The confirmation payload containing external payment gateway reference.</param>
    /// <param name="cancellationToken">Cancellation token for asynchronous operation.</param>
    /// <response code="200">Payment confirmed successfully.</response>
    /// <response code="400">Payment confirmation failed or invalid state transition.</response>
    /// <response code="401">Unauthorized if the request lacks a valid Bearer token.</response>
    /// <response code="403">Forbidden if the caller lacks the 'pledges:payments:manage' permission.</response>
    /// <response code="404">Pledge or campaign not found.</response>
    /// <response code="409">Pledge was concurrently modified by another request.</response>
    [Authorize(Policy = PermissionConstants.PledgesPaymentsManage)]
    [HttpPost("{pledgeId:guid}/confirm-payment")]
    [ProducesResponseType(typeof(ConfirmPledgePaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ConfirmPledgePaymentResponse>> ConfirmPayment(
        [FromRoute] Guid campaignId,
        [FromRoute] Guid pledgeId,
        [FromBody] ConfirmPledgePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ConfirmPledgePaymentCommand(campaignId, pledgeId, request.PaymentReference);
        var validationResult = await _confirmPaymentValidator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        var result = await _commandDispatcher.SendAsync<ConfirmPledgePaymentResult>(command, cancellationToken);
        return Ok(_mapper.Map<ConfirmPledgePaymentResponse>(result));
    }

    /// <summary>
    /// Records that the backer's card was saved (save-then-charge funding model): the pledge becomes active and
    /// starts to count toward the campaign. Nothing is charged here; the card is charged once at the deadline if the
    /// campaign is funded. In production this is the payment provider's "setup succeeded" news.
    /// </summary>
    /// <param name="campaignId">The unique identifier of the campaign.</param>
    /// <param name="pledgeId">The unique identifier of the pledge.</param>
    /// <param name="request">The provider's reference for the saved card.</param>
    /// <param name="cancellationToken">Cancellation token for asynchronous operation.</param>
    /// <response code="200">The pledge is active.</response>
    /// <response code="400">Invalid request or the pledge cannot be activated.</response>
    /// <response code="401">Unauthorized if the request lacks a valid Bearer token.</response>
    /// <response code="403">Forbidden if the caller lacks the 'pledges:payments:manage' permission.</response>
    /// <response code="404">Pledge or campaign not found.</response>
    [Authorize(Policy = PermissionConstants.PledgesPaymentsManage)]
    [HttpPost("{pledgeId:guid}/confirm-setup")]
    [ProducesResponseType(typeof(ConfirmPledgeSetupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConfirmPledgeSetupResponse>> ConfirmSetup(
        [FromRoute] Guid campaignId,
        [FromRoute] Guid pledgeId,
        [FromBody] ConfirmPledgeSetupRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ConfirmPledgeSetupCommand(campaignId, pledgeId, request.PaymentMethodReference);
        var validationResult = await _confirmSetupValidator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        var result = await _commandDispatcher.SendAsync<ConfirmPledgeSetupResult>(command, cancellationToken);
        return Ok(_mapper.Map<ConfirmPledgeSetupResponse>(result));
    }

    /// <summary>
    /// Marks a pending pledge as failed due to payment processing issues.
    /// </summary>
    /// <param name="campaignId">The unique identifier of the campaign.</param>
    /// <param name="pledgeId">The unique identifier of the pledge to mark as failed.</param>
    /// <param name="request">The failure details payload containing the reason for failure.</param>
    /// <param name="cancellationToken">Cancellation token for asynchronous operation.</param>
    /// <response code="200">Pledge marked as failed.</response>
    /// <response code="400">Validation error or invalid state transition.</response>
    /// <response code="401">Unauthorized if the request lacks a valid Bearer token.</response>
    /// <response code="403">Forbidden if the caller lacks the 'pledges:payments:manage' permission.</response>
    /// <response code="404">Pledge or campaign not found.</response>
    /// <response code="409">Pledge was concurrently modified by another request.</response>
    [Authorize(Policy = PermissionConstants.PledgesPaymentsManage)]
    [HttpPost("{pledgeId:guid}/fail-payment")]
    [ProducesResponseType(typeof(FailPledgePaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FailPledgePaymentResponse>> FailPayment(
        [FromRoute] Guid campaignId,
        [FromRoute] Guid pledgeId,
        [FromBody] FailPledgePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var command = new FailPledgePaymentCommand(campaignId, pledgeId, request.FailureReason);
        var validationResult = await _failPaymentValidator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        var result = await _commandDispatcher.SendAsync<FailPledgePaymentResult>(command, cancellationToken);
        return Ok(_mapper.Map<FailPledgePaymentResponse>(result));
    }
}

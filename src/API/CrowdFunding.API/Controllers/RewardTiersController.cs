using CrowdFunding.API.Contracts.RewardTiers;
using CrowdFunding.API.Validation;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Campaigns.Application.Features.RewardTiers.Commands.CreateRewardTier;
using CrowdFunding.Modules.Campaigns.Application.Features.RewardTiers.Commands.ReserveRewardTierSlot;
using CrowdFunding.Modules.Campaigns.Application.Features.RewardTiers.Queries.GetRewardTierById;
using CrowdFunding.Modules.Campaigns.Application.Features.RewardTiers.Queries.ListRewardTiersByCampaign;
using FluentValidation;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrowdFunding.API.Controllers;

/// <summary>
/// Exposes HTTP endpoints for a campaign's limited-quantity reward perk tiers. See TICKET-034.
/// </summary>
[ApiController]
[Route("api/campaigns/{campaignId:guid}/reward-tiers")]
[Tags("RewardTiers")]
[Authorize]
public sealed class RewardTiersController : ControllerBase
{
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly IQueryDispatcher _queryDispatcher;
    private readonly IValidator<CreateRewardTierCommand> _createValidator;
    private readonly IValidator<ReserveRewardTierSlotCommand> _reserveValidator;
    private readonly IMapper _mapper;

    public RewardTiersController(
        ICommandDispatcher commandDispatcher,
        IQueryDispatcher queryDispatcher,
        IValidator<CreateRewardTierCommand> createValidator,
        IValidator<ReserveRewardTierSlotCommand> reserveValidator,
        IMapper mapper)
    {
        _commandDispatcher = commandDispatcher;
        _queryDispatcher = queryDispatcher;
        _createValidator = createValidator;
        _reserveValidator = reserveValidator;
        _mapper = mapper;
    }

    /// <summary>
    /// Defines a new reward tier for a campaign. Only the campaign's owner may do this.
    /// </summary>
    /// <response code="201">Reward tier created.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="403">The current user does not own this campaign.</response>
    /// <response code="404">Campaign not found.</response>
    [HttpPost]
    [ProducesResponseType(typeof(CreateRewardTierResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreateRewardTierResponse>> Create(
        [FromRoute] Guid campaignId,
        [FromBody] CreateRewardTierRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateRewardTierCommand(
            campaignId, request.Title, request.Description, request.MinimumPledgeAmount, request.Currency, request.TotalCapacity);
        var validationResult = await _createValidator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        var result = await _commandDispatcher.SendAsync<CreateRewardTierResult>(command, cancellationToken);
        var response = _mapper.Map<CreateRewardTierResponse>(result);

        // RFC 9110 §10.2.2: the Location header must resolve — generated from the route
        // collection via the GetById action below rather than a hand-built string, so it can
        // never drift out of sync with the actual GET route (TICKET-047).
        return CreatedAtAction(nameof(GetById), new { campaignId, rewardTierId = response.RewardTierId }, response);
    }

    /// <summary>
    /// Lists every reward tier defined for a campaign, with remaining capacity.
    /// </summary>
    /// <response code="200">Returns the campaign's reward tiers.</response>
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(typeof(ListRewardTiersResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ListRewardTiersResponse>> List(
        [FromRoute] Guid campaignId,
        CancellationToken cancellationToken)
    {
        var result = await _queryDispatcher.QueryAsync<ListRewardTiersByCampaignResult>(
            new ListRewardTiersByCampaignQuery(campaignId), cancellationToken);

        return Ok(_mapper.Map<ListRewardTiersResponse>(result));
    }

    /// <summary>
    /// Retrieves a single reward tier by its unique identifier.
    /// </summary>
    /// <response code="200">Returns the reward tier.</response>
    /// <response code="404">Reward tier not found for the given campaign.</response>
    [AllowAnonymous]
    [HttpGet("{rewardTierId:guid}")]
    [ProducesResponseType(typeof(RewardTierResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RewardTierResponse>> GetById(
        [FromRoute] Guid campaignId,
        [FromRoute] Guid rewardTierId,
        CancellationToken cancellationToken)
    {
        var result = await _queryDispatcher.QueryAsync<GetRewardTierByIdResult>(
            new GetRewardTierByIdQuery(campaignId, rewardTierId), cancellationToken);

        return Ok(_mapper.Map<RewardTierResponse>(result));
    }

    /// <summary>
    /// Reserves one slot on a reward tier for the current backer.
    /// </summary>
    /// <response code="200">Slot reserved.</response>
    /// <response code="400">Validation failed, or the reward tier is completely sold out (a
    /// domain state-invariant violation — same convention as every other "can't transition from
    /// this state" rejection in this codebase, e.g. confirming an already-confirmed payment).</response>
    /// <response code="404">Reward tier not found.</response>
    [HttpPost("{rewardTierId:guid}/reserve")]
    [ProducesResponseType(typeof(ReserveRewardTierSlotResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReserveRewardTierSlotResponse>> Reserve(
        [FromRoute] Guid campaignId,
        [FromRoute] Guid rewardTierId,
        CancellationToken cancellationToken)
    {
        var command = new ReserveRewardTierSlotCommand(campaignId, rewardTierId);
        var validationResult = await _reserveValidator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        var result = await _commandDispatcher.SendAsync<ReserveRewardTierSlotResult>(command, cancellationToken);
        return Ok(_mapper.Map<ReserveRewardTierSlotResponse>(result));
    }
}

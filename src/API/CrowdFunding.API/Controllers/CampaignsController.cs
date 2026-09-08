using CrowdFunding.API.Contracts.Common;
using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.API.Validation;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Pagination;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CancelCampaign;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CreateCampaign;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.PublishCampaign;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.GetCampaignById;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.ListCampaigns;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using FluentValidation;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrowdFunding.API.Controllers;

/// <summary>
/// Exposes HTTP endpoints for Campaigns: creation, listing, details, publishing, and cancellation.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Tags("Campaigns")]
public sealed class CampaignsController : ControllerBase
{
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly IQueryDispatcher _queryDispatcher;
    private readonly IMapper _mapper;
    private readonly IValidator<CancelCampaignCommand> _cancelCampaignValidator;
    private readonly IValidator<CreateCampaignCommand> _createCampaignValidator;
    private readonly IValidator<PublishCampaignCommand> _publishCampaignValidator;

    public CampaignsController(
        ICommandDispatcher commandDispatcher,
        IQueryDispatcher queryDispatcher,
        IMapper mapper,
        IValidator<CancelCampaignCommand> cancelCampaignValidator,
        IValidator<CreateCampaignCommand> createCampaignValidator,
        IValidator<PublishCampaignCommand> publishCampaignValidator)
    {
        _commandDispatcher = commandDispatcher;
        _queryDispatcher = queryDispatcher;
        _mapper = mapper;
        _cancelCampaignValidator = cancelCampaignValidator;
        _createCampaignValidator = createCampaignValidator;
        _publishCampaignValidator = publishCampaignValidator;
    }

    /// <summary>
    /// Retrieves a paginated list of campaigns with optional filters.
    /// </summary>
    /// <param name="pageNumber">The page number to retrieve (1-based, default: 1).</param>
    /// <param name="pageSize">The number of items per page (default: 10).</param>
    /// <param name="ownerId">Optional filter for campaigns owned by a specific user.</param>
    /// <param name="category">Optional category filter.</param>
    /// <param name="status">Optional status filter (Draft, Published, Completed, Cancelled).</param>
    /// <param name="cancellationToken">Cancellation token for asynchronous operation.</param>
    /// <response code="200">Returns the requested page of campaigns.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ListCampaignsResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ListCampaignsResponse>>> List(
        [FromQuery] int? pageNumber,
        [FromQuery] int? pageSize,
        [FromQuery] Guid? ownerId,
        [FromQuery] string? category,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var pageRequest = PageRequest.Create(pageNumber, pageSize);
        var filter = new ListCampaignsFilter(ownerId, category, status);
        var result = await _queryDispatcher.QueryAsync<PagedResult<ListCampaignsResult>>(
            new ListCampaignsQuery(pageRequest, filter),
            cancellationToken);

        var items = result.Items.Select(x => _mapper.Map<ListCampaignsResponse>(x)).ToArray();
        var response = new PagedResponse<ListCampaignsResponse>(
            items,
            result.PageNumber,
            result.PageSize,
            result.TotalCount,
            result.TotalPages);

        return Ok(response);
    }

    /// <summary>
    /// Creates a new crowdfunding campaign in Draft status.
    /// </summary>
    /// <param name="request">The campaign creation payload including title, story, category, goal amount, currency, and deadline.</param>
    /// <param name="cancellationToken">Cancellation token for asynchronous operation.</param>
    /// <response code="201">Campaign created successfully in Draft status.</response>
    /// <response code="400">Invalid campaign parameters or validation errors.</response>
    /// <response code="401">Unauthorized if the request lacks a valid Bearer token.</response>
    /// <response code="403">Forbidden if the caller lacks the 'campaigns:create' permission.</response>
    [Authorize(Policy = PermissionConstants.CampaignsCreate)]
    [HttpPost]
    [ProducesResponseType(typeof(CreateCampaignResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCampaignRequest request,
        CancellationToken cancellationToken)
    {
        var command = _mapper.Map<CreateCampaignCommand>(request);
        var validationResult = await _createCampaignValidator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        var result = await _commandDispatcher.SendAsync<CreateCampaignResult>(command, cancellationToken);
        var response = _mapper.Map<CreateCampaignResponse>(result);

        return CreatedAtAction(nameof(GetById), new { id = response.CampaignId }, response);
    }

    /// <summary>
    /// Publishes an approved campaign, making it active for contributions.
    /// </summary>
    /// <param name="id">The unique identifier of the campaign to publish.</param>
    /// <param name="cancellationToken">Cancellation token for asynchronous operation.</param>
    /// <response code="200">Campaign published successfully.</response>
    /// <response code="400">Campaign cannot be published (e.g. not approved or invalid state).</response>
    /// <response code="401">Unauthorized if the request lacks a valid Bearer token.</response>
    /// <response code="403">Forbidden if the caller lacks the 'campaigns:publish' permission.</response>
    /// <response code="404">Campaign not found.</response>
    [Authorize(Policy = PermissionConstants.CampaignsPublish)]
    [HttpPost("{id:guid}/publish")]
    [ProducesResponseType(typeof(PublishCampaignResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublishCampaignResponse>> Publish([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var command = new PublishCampaignCommand(id);
        var validationResult = await _publishCampaignValidator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        var result = await _commandDispatcher.SendAsync<PublishCampaignResult>(command, cancellationToken);
        return Ok(_mapper.Map<PublishCampaignResponse>(result));
    }

    /// <summary>
    /// Cancels an ongoing campaign.
    /// </summary>
    /// <param name="id">The unique identifier of the campaign to cancel.</param>
    /// <param name="cancellationToken">Cancellation token for asynchronous operation.</param>
    /// <response code="200">Campaign cancelled successfully.</response>
    /// <response code="400">Campaign cannot be cancelled in its current state.</response>
    /// <response code="401">Unauthorized if the request lacks a valid Bearer token.</response>
    /// <response code="403">Forbidden if the caller lacks the 'campaigns:cancel' permission.</response>
    /// <response code="404">Campaign not found.</response>
    [Authorize(Policy = PermissionConstants.CampaignsCancel)]
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(CancelCampaignResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CancelCampaignResponse>> Cancel([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var command = new CancelCampaignCommand(id);
        var validationResult = await _cancelCampaignValidator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        var result = await _commandDispatcher.SendAsync<CancelCampaignResult>(command, cancellationToken);
        return Ok(_mapper.Map<CancelCampaignResponse>(result));
    }

    /// <summary>
    /// Retrieves campaign details by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the campaign.</param>
    /// <param name="cancellationToken">Cancellation token for asynchronous operation.</param>
    /// <response code="200">Returns the campaign details.</response>
    /// <response code="404">Campaign not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GetCampaignByIdResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetCampaignByIdResponse>> GetById([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await _queryDispatcher.QueryAsync<GetCampaignByIdResult>(new GetCampaignByIdQuery(id), cancellationToken);
        return Ok(_mapper.Map<GetCampaignByIdResponse>(result));
    }
}

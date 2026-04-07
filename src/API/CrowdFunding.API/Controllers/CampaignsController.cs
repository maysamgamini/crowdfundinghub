using CrowdFunding.API.Contracts.Common;
using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.BuildingBlocks.Application.Pagination;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CancelCampaign;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CreateCampaign;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.PublishCampaign;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.GetCampaignById;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.ListCampaigns;
using FluentValidation;
using MapsterMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrowdFunding.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CampaignsController : ControllerBase
{
    private readonly CancelCampaignCommandHandler _cancelCampaignCommandHandler;
    private readonly CreateCampaignCommandHandler _createCampaignCommandHandler;
    private readonly PublishCampaignCommandHandler _publishCampaignCommandHandler;
    private readonly GetCampaignByIdQueryHandler _getCampaignByIdQueryHandler;
    private readonly ListCampaignsQueryHandler _listCampaignsQueryHandler;
    private readonly IMapper _mapper;
    private readonly IValidator<CancelCampaignCommand> _cancelCampaignValidator;
    private readonly IValidator<CreateCampaignCommand> _createCampaignValidator;
    private readonly IValidator<PublishCampaignCommand> _publishCampaignValidator;

    public CampaignsController(
        CancelCampaignCommandHandler cancelCampaignCommandHandler,
        CreateCampaignCommandHandler createCampaignCommandHandler,
        PublishCampaignCommandHandler publishCampaignCommandHandler,
        GetCampaignByIdQueryHandler getCampaignByIdQueryHandler,
        ListCampaignsQueryHandler listCampaignsQueryHandler,
        IMapper mapper,
        IValidator<CancelCampaignCommand> cancelCampaignValidator,
        IValidator<CreateCampaignCommand> createCampaignValidator,
        IValidator<PublishCampaignCommand> publishCampaignValidator)
    {
        _cancelCampaignCommandHandler = cancelCampaignCommandHandler;
        _createCampaignCommandHandler = createCampaignCommandHandler;
        _publishCampaignCommandHandler = publishCampaignCommandHandler;
        _getCampaignByIdQueryHandler = getCampaignByIdQueryHandler;
        _listCampaignsQueryHandler = listCampaignsQueryHandler;
        _mapper = mapper;
        _cancelCampaignValidator = cancelCampaignValidator;
        _createCampaignValidator = createCampaignValidator;
        _publishCampaignValidator = publishCampaignValidator;
    }

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
        var result = await _listCampaignsQueryHandler.Handle(
            new ListCampaignsQuery(pageRequest, filter),
            cancellationToken);
        var items = result.Items
            .Select(x => _mapper.Map<ListCampaignsResponse>(x))
            .ToArray();

        var response = new PagedResponse<ListCampaignsResponse>(
            items,
            result.PageNumber,
            result.PageSize,
            result.TotalCount,
            result.TotalPages);

        return Ok(response);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreateCampaignResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
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

        var result = await _createCampaignCommandHandler.Handle(command, cancellationToken);
        var response = _mapper.Map<CreateCampaignResponse>(result);

        return CreatedAtAction(
            nameof(GetById),
            new { id = response.CampaignId },
            response);
    }

    [HttpPost("{id:guid}/publish")]
    [ProducesResponseType(typeof(PublishCampaignResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublishCampaignResponse>> Publish(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new PublishCampaignCommand(id);

        var validationResult = await _publishCampaignValidator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        var result = await _publishCampaignCommandHandler.Handle(command, cancellationToken);
        var response = _mapper.Map<PublishCampaignResponse>(result);

        return Ok(response);
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(CancelCampaignResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CancelCampaignResponse>> Cancel(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new CancelCampaignCommand(id);

        var validationResult = await _cancelCampaignValidator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        var result = await _cancelCampaignCommandHandler.Handle(command, cancellationToken);
        var response = _mapper.Map<CancelCampaignResponse>(result);

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GetCampaignByIdResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetCampaignByIdResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetCampaignByIdQuery(id);

        var result = await _getCampaignByIdQueryHandler.Handle(query, cancellationToken);

        var response = _mapper.Map<GetCampaignByIdResponse>(result);

        return Ok(response);
    }
}

internal static class ValidationExtensions
{
    public static void AddToModelState(
        this FluentValidation.Results.ValidationResult validationResult,
        ModelStateDictionary modelState)
    {
        foreach (var error in validationResult.Errors)
        {
            modelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }
    }
}

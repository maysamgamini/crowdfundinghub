using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CreateCampaign;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.PublishCampaign;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.GetCampaignById;
using FluentValidation;
using MapsterMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrowdFunding.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CampaignsController : ControllerBase
{
    private readonly CreateCampaignCommandHandler _createCampaignCommandHandler;
    private readonly PublishCampaignCommandHandler _publishCampaignCommandHandler;
    private readonly GetCampaignByIdQueryHandler _getCampaignByIdQueryHandler;
    private readonly IMapper _mapper;
    private readonly IValidator<CreateCampaignCommand> _createCampaignValidator;
    private readonly IValidator<PublishCampaignCommand> _publishCampaignValidator;

    public CampaignsController(
        CreateCampaignCommandHandler createCampaignCommandHandler,
        PublishCampaignCommandHandler publishCampaignCommandHandler,
        GetCampaignByIdQueryHandler getCampaignByIdQueryHandler,
        IMapper mapper,
        IValidator<CreateCampaignCommand> createCampaignValidator,
        IValidator<PublishCampaignCommand> publishCampaignValidator)
    {
        _createCampaignCommandHandler = createCampaignCommandHandler;
        _publishCampaignCommandHandler = publishCampaignCommandHandler;
        _getCampaignByIdQueryHandler = getCampaignByIdQueryHandler;
        _mapper = mapper;
        _createCampaignValidator = createCampaignValidator;
        _publishCampaignValidator = publishCampaignValidator;
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

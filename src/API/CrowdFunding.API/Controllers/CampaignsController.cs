using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CreateCampaign;
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
    private readonly IMapper _mapper;
    private readonly IValidator<CreateCampaignCommand> _validator;

    public CampaignsController(
        CreateCampaignCommandHandler createCampaignCommandHandler,
        IMapper mapper,
        IValidator<CreateCampaignCommand> validator)
    {
        _createCampaignCommandHandler = createCampaignCommandHandler;
        _mapper = mapper;
        _validator = validator;
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreateCampaignResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCampaignRequest request,
        CancellationToken cancellationToken)
    {
        var command = _mapper.Map<CreateCampaignCommand>(request);

        var validationResult = await _validator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        var result = await _createCampaignCommandHandler.Handle(command, cancellationToken);

        var response = _mapper.Map<CreateCampaignResponse>(result);

        return CreatedAtAction(
            nameof(GetByIdPlaceholder),
            new { id = response.CampaignId },
            response);
    }

    [HttpGet("{id:guid}")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult GetByIdPlaceholder(Guid id)
    {
        return Ok();
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
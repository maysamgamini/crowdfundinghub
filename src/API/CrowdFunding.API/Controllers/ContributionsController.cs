using CrowdFunding.API.Contracts.Contributions;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.MakeContribution;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Queries.ListContributionsByCampaign;
using FluentValidation;
using MapsterMapper;
using Microsoft.AspNetCore.Mvc;

namespace CrowdFunding.API.Controllers;

[ApiController]
[Route("api/campaigns/{campaignId:guid}/[controller]")]
public sealed class ContributionsController : ControllerBase
{
    private readonly MakeContributionCommandHandler _makeContributionCommandHandler;
    private readonly ListContributionsByCampaignQueryHandler _listContributionsByCampaignQueryHandler;
    private readonly IMapper _mapper;
    private readonly IValidator<MakeContributionCommand> _validator;

    public ContributionsController(
        MakeContributionCommandHandler makeContributionCommandHandler,
        ListContributionsByCampaignQueryHandler listContributionsByCampaignQueryHandler,
        IMapper mapper,
        IValidator<MakeContributionCommand> validator)
    {
        _makeContributionCommandHandler = makeContributionCommandHandler;
        _listContributionsByCampaignQueryHandler = listContributionsByCampaignQueryHandler;
        _mapper = mapper;
        _validator = validator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<ListContributionsResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ListContributionsResponse>>> ListByCampaign(
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        var result = await _listContributionsByCampaignQueryHandler.Handle(
            new ListContributionsByCampaignQuery(campaignId),
            cancellationToken);

        var response = result
            .Select(x => _mapper.Map<ListContributionsResponse>(x))
            .ToArray();

        return Ok(response);
    }

    [HttpPost]
    [ProducesResponseType(typeof(MakeContributionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MakeContributionResponse>> Create(
        Guid campaignId,
        [FromBody] MakeContributionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new MakeContributionCommand(
            campaignId,
            request.ContributorId,
            request.Amount,
            request.Currency);

        var validationResult = await _validator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        var result = await _makeContributionCommandHandler.Handle(command, cancellationToken);
        var response = _mapper.Map<MakeContributionResponse>(result);

        return CreatedAtAction(
            nameof(ListByCampaign),
            new { campaignId },
            response);
    }
}

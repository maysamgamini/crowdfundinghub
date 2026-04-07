using CrowdFunding.API.Contracts.Common;
using CrowdFunding.API.Contracts.Contributions;
using CrowdFunding.BuildingBlocks.Application.Pagination;
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
    [ProducesResponseType(typeof(PagedResponse<ListContributionsResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ListContributionsResponse>>> ListByCampaign(
        Guid campaignId,
        [FromQuery] int? pageNumber,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var pageRequest = PageRequest.Create(pageNumber, pageSize);
        var result = await _listContributionsByCampaignQueryHandler.Handle(
            new ListContributionsByCampaignQuery(campaignId, pageRequest),
            cancellationToken);

        var items = result.Items
            .Select(x => _mapper.Map<ListContributionsResponse>(x))
            .ToArray();

        var response = new PagedResponse<ListContributionsResponse>(
            items,
            result.PageNumber,
            result.PageSize,
            result.TotalCount,
            result.TotalPages);

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

using CrowdFunding.API.Contracts.Common;
using CrowdFunding.API.Contracts.Contributions;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Pagination;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.ConfirmContributionPayment;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.FailContributionPayment;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.MakeContribution;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Queries.ListContributionsByCampaign;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using FluentValidation;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrowdFunding.API.Controllers;

[ApiController]
[Route("api/campaigns/{campaignId:guid}/[controller]")]
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

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ListContributionsResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ListContributionsResponse>>> ListByCampaign(
        Guid campaignId,
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

    [Authorize(Policy = PermissionConstants.CampaignsContribute)]
    [HttpPost]
    [ProducesResponseType(typeof(MakeContributionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MakeContributionResponse>> Create(
        Guid campaignId,
        [FromBody] MakeContributionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new MakeContributionCommand(campaignId, request.Amount, request.Currency);
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        var result = await _commandDispatcher.SendAsync<MakeContributionResult>(command, cancellationToken);
        var response = _mapper.Map<MakeContributionResponse>(result);

        return CreatedAtAction(nameof(ListByCampaign), new { campaignId }, response);
    }

    [Authorize(Policy = PermissionConstants.ContributionsPaymentsManage)]
    [HttpPost("{contributionId:guid}/confirm-payment")]
    [ProducesResponseType(typeof(ConfirmContributionPaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConfirmContributionPaymentResponse>> ConfirmPayment(
        Guid campaignId,
        Guid contributionId,
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

    [Authorize(Policy = PermissionConstants.ContributionsPaymentsManage)]
    [HttpPost("{contributionId:guid}/fail-payment")]
    [ProducesResponseType(typeof(FailContributionPaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FailContributionPaymentResponse>> FailPayment(
        Guid campaignId,
        Guid contributionId,
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
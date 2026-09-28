using CrowdFunding.API.Contracts.Common;
using CrowdFunding.API.Contracts.Contributions;
using CrowdFunding.API.RateLimiting;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Pagination;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.ConfirmContributionPayment;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.FailContributionPayment;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.MakeContribution;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Queries.GetContributionById;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Queries.ListContributionsByCampaign;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CrowdFunding.API.Endpoints;

/// <summary>
/// Contribution endpoints: pledges to a campaign and their manual payment confirmation or failure
/// (the payment gateway normally reports outcomes through the inbound webhook instead).
/// </summary>
public static class ContributionEndpoints
{
    private const string GetContributionByIdRoute = "GetContributionById";

    /// <summary>
    /// Maps the <c>/api/campaigns/{campaignId}/contributions</c> group.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The route group.</returns>
    public static RouteGroupBuilder MapContributionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/campaigns/{campaignId:guid}/contributions").WithTags("Contributions");

        group.MapGet("/", ListAsync)
            .AllowAnonymous();

        group.MapGet("/{contributionId:guid}", GetByIdAsync)
            .AllowAnonymous()
            .WithName(GetContributionByIdRoute)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateAsync)
            .RequireAuthorization(PermissionConstants.CampaignsContribute)
            .RequireRateLimiting(RateLimitingConfiguration.PaymentPolicy)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/{contributionId:guid}/confirm-payment", ConfirmPaymentAsync)
            .RequireAuthorization(PermissionConstants.ContributionsPaymentsManage)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{contributionId:guid}/fail-payment", FailPaymentAsync)
            .RequireAuthorization(PermissionConstants.ContributionsPaymentsManage)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    /// <summary>
    /// Lists a campaign's contributions, optionally filtered by contributor, currency and status.
    /// </summary>
    /// <param name="campaignId">The campaign id.</param>
    /// <param name="pageNumber">The 1-based page number (default 1).</param>
    /// <param name="pageSize">The page size (default 20).</param>
    /// <param name="contributorId">Only contributions by this user.</param>
    /// <param name="currency">Only contributions in this currency.</param>
    /// <param name="status">Only contributions in this status (Pending, Succeeded, Failed, Refunded).</param>
    /// <param name="dispatcher">The query dispatcher.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>One page of contributions.</returns>
    internal static async Task<Ok<PagedResponse<ListContributionsResponse>>> ListAsync(
        Guid campaignId,
        int? pageNumber,
        int? pageSize,
        Guid? contributorId,
        string? currency,
        string? status,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var page = await dispatcher.QueryAsync<PagedResult<ListContributionsByCampaignResult>>(
            new ListContributionsByCampaignQuery(
                campaignId, PageRequest.Create(pageNumber, pageSize), new ListContributionsByCampaignFilter(contributorId, currency, status)),
            cancellationToken);

        return TypedResults.Ok(PagedResponse.From(page, contribution => new ListContributionsResponse(
            contribution.Id, contribution.CampaignId, contribution.ContributorId, contribution.Amount, contribution.Currency,
            contribution.Status, contribution.PaymentReference, contribution.FailureReason, contribution.CreatedAtUtc,
            contribution.ProcessedAtUtc)));
    }

    /// <summary>
    /// Returns one contribution.
    /// </summary>
    /// <param name="campaignId">The campaign id.</param>
    /// <param name="contributionId">The contribution id.</param>
    /// <param name="dispatcher">The query dispatcher.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The contribution.</returns>
    internal static async Task<Ok<GetContributionByIdResponse>> GetByIdAsync(
        Guid campaignId, Guid contributionId, IQueryDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var contribution = await dispatcher.QueryAsync<GetContributionByIdResult>(
            new GetContributionByIdQuery(campaignId, contributionId), cancellationToken);

        return TypedResults.Ok(new GetContributionByIdResponse(
            contribution.Id, contribution.CampaignId, contribution.ContributorId, contribution.Amount, contribution.Currency,
            contribution.Status, contribution.PaymentReference, contribution.FailureReason, contribution.CreatedAtUtc,
            contribution.ProcessedAtUtc, contribution.ExternalPaymentIntentId));
    }

    /// <summary>
    /// Pledges an amount to a published campaign, optionally claiming a reserved reward tier slot.
    /// The contribution starts Pending until the payment gateway reports the outcome.
    /// </summary>
    /// <param name="campaignId">The campaign id.</param>
    /// <param name="request">The amount, currency and optional reward tier reservation.</param>
    /// <param name="dispatcher">The command dispatcher.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The new contribution, with a Location header pointing at it.</returns>
    internal static async Task<CreatedAtRoute<MakeContributionResponse>> CreateAsync(
        Guid campaignId, MakeContributionRequest request, ICommandDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync<MakeContributionResult>(
            new MakeContributionCommand(campaignId, request.Amount, request.Currency, request.RewardTierReservationId),
            cancellationToken);

        // Location points at the contribution itself (RFC 9110 §10.3.2), not the campaign's list.
        return TypedResults.CreatedAtRoute(
            new MakeContributionResponse(result.ContributionId, result.Status),
            GetContributionByIdRoute,
            new { campaignId, contributionId = result.ContributionId });
    }

    /// <summary>
    /// Manually confirms a pending contribution's payment.
    /// </summary>
    /// <param name="campaignId">The campaign id.</param>
    /// <param name="contributionId">The contribution id.</param>
    /// <param name="request">The payment reference.</param>
    /// <param name="dispatcher">The command dispatcher.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The contribution's new status.</returns>
    internal static async Task<Ok<ConfirmContributionPaymentResponse>> ConfirmPaymentAsync(
        Guid campaignId, Guid contributionId, ConfirmContributionPaymentRequest request, ICommandDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync<ConfirmContributionPaymentResult>(
            new ConfirmContributionPaymentCommand(campaignId, contributionId, request.PaymentReference), cancellationToken);

        return TypedResults.Ok(new ConfirmContributionPaymentResponse(result.ContributionId, result.Status, result.PaymentReference));
    }

    /// <summary>
    /// Manually marks a pending contribution's payment as failed.
    /// </summary>
    /// <param name="campaignId">The campaign id.</param>
    /// <param name="contributionId">The contribution id.</param>
    /// <param name="request">The failure reason.</param>
    /// <param name="dispatcher">The command dispatcher.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The contribution's new status.</returns>
    internal static async Task<Ok<FailContributionPaymentResponse>> FailPaymentAsync(
        Guid campaignId, Guid contributionId, FailContributionPaymentRequest request, ICommandDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync<FailContributionPaymentResult>(
            new FailContributionPaymentCommand(campaignId, contributionId, request.FailureReason), cancellationToken);

        return TypedResults.Ok(new FailContributionPaymentResponse(result.ContributionId, result.Status, result.FailureReason));
    }
}

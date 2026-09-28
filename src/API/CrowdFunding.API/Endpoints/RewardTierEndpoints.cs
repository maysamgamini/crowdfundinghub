using CrowdFunding.API.Contracts.RewardTiers;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Campaigns.Application.Features.RewardTiers.Commands.CreateRewardTier;
using CrowdFunding.Modules.Campaigns.Application.Features.RewardTiers.Commands.ReserveRewardTierSlot;
using CrowdFunding.Modules.Campaigns.Application.Features.RewardTiers.Queries.GetRewardTierById;
using CrowdFunding.Modules.Campaigns.Application.Features.RewardTiers.Queries.ListRewardTiersByCampaign;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CrowdFunding.API.Endpoints;

/// <summary>
/// Reward tier endpoints: limited-capacity perks a campaign owner defines and backers reserve.
/// </summary>
public static class RewardTierEndpoints
{
    private const string GetRewardTierByIdRoute = "GetRewardTierById";

    /// <summary>
    /// Maps the <c>/api/campaigns/{campaignId}/reward-tiers</c> group.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The route group.</returns>
    public static RouteGroupBuilder MapRewardTierEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/campaigns/{campaignId:guid}/reward-tiers")
            .WithTags("RewardTiers")
            .RequireAuthorization();

        group.MapGet("/", ListAsync)
            .AllowAnonymous()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{rewardTierId:guid}", GetByIdAsync)
            .AllowAnonymous()
            .WithName(GetRewardTierByIdRoute)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateAsync)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{rewardTierId:guid}/reserve", ReserveAsync)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    /// <summary>
    /// Lists a campaign's reward tiers with their remaining capacity.
    /// </summary>
    /// <param name="campaignId">The campaign id.</param>
    /// <param name="dispatcher">The query dispatcher.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The campaign's reward tiers.</returns>
    internal static async Task<Ok<ListRewardTiersResponse>> ListAsync(
        Guid campaignId, IQueryDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.QueryAsync<ListRewardTiersByCampaignResult>(new ListRewardTiersByCampaignQuery(campaignId), cancellationToken);

        return TypedResults.Ok(new ListRewardTiersResponse(result.RewardTiers.Select(tier => new RewardTierResponse(
            tier.RewardTierId, tier.CampaignId, tier.Title, tier.Description, tier.MinimumPledgeAmount, tier.MinimumPledgeCurrency,
            tier.TotalCapacity, tier.ClaimedCount, tier.ReservedCount, tier.AvailableCount, tier.CreatedAtUtc)).ToList()));
    }

    /// <summary>
    /// Returns one reward tier.
    /// </summary>
    /// <param name="campaignId">The campaign id.</param>
    /// <param name="rewardTierId">The reward tier id.</param>
    /// <param name="dispatcher">The query dispatcher.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The reward tier.</returns>
    internal static async Task<Ok<RewardTierResponse>> GetByIdAsync(
        Guid campaignId, Guid rewardTierId, IQueryDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var tier = await dispatcher.QueryAsync<GetRewardTierByIdResult>(new GetRewardTierByIdQuery(campaignId, rewardTierId), cancellationToken);

        return TypedResults.Ok(new RewardTierResponse(
            tier.RewardTierId, tier.CampaignId, tier.Title, tier.Description, tier.MinimumPledgeAmount, tier.MinimumPledgeCurrency,
            tier.TotalCapacity, tier.ClaimedCount, tier.ReservedCount, tier.AvailableCount, tier.CreatedAtUtc));
    }

    /// <summary>
    /// Defines a new reward tier. Only the campaign's owner may do this.
    /// </summary>
    /// <param name="campaignId">The campaign id.</param>
    /// <param name="request">The tier's title, description, minimum pledge and capacity.</param>
    /// <param name="dispatcher">The command dispatcher.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The new tier, with a Location header pointing at it.</returns>
    internal static async Task<CreatedAtRoute<CreateRewardTierResponse>> CreateAsync(
        Guid campaignId, CreateRewardTierRequest request, ICommandDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync<CreateRewardTierResult>(
            new CreateRewardTierCommand(campaignId, request.Title, request.Description, request.MinimumPledgeAmount, request.Currency, request.TotalCapacity),
            cancellationToken);

        return TypedResults.CreatedAtRoute(
            new CreateRewardTierResponse(result.RewardTierId, result.CampaignId, result.Title, result.TotalCapacity, result.AvailableCount),
            GetRewardTierByIdRoute,
            new { campaignId, rewardTierId = result.RewardTierId });
    }

    /// <summary>
    /// Reserves one slot of a reward tier for the caller, to be claimed by a contribution before the
    /// reservation expires.
    /// </summary>
    /// <param name="campaignId">The campaign id.</param>
    /// <param name="rewardTierId">The reward tier id.</param>
    /// <param name="dispatcher">The command dispatcher.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The reservation id and the tier's remaining capacity.</returns>
    internal static async Task<Ok<ReserveRewardTierSlotResponse>> ReserveAsync(
        Guid campaignId, Guid rewardTierId, ICommandDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync<ReserveRewardTierSlotResult>(
            new ReserveRewardTierSlotCommand(campaignId, rewardTierId), cancellationToken);

        return TypedResults.Ok(new ReserveRewardTierSlotResponse(result.RewardTierId, result.AvailableCount, result.ReservationId));
    }
}

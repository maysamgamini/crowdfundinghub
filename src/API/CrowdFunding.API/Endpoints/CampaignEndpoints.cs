using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.API.Contracts.Common;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Pagination;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CancelCampaign;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CreateCampaign;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.PublishCampaign;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.GetCampaignById;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.ListCampaigns;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CrowdFunding.API.Endpoints;

/// <summary>
/// Campaign endpoints: public discovery plus the owner/administrator lifecycle (create, publish,
/// cancel). Compare with <c>Controllers/ModerationController</c>, the same shape written as an
/// MVC controller.
/// </summary>
public static class CampaignEndpoints
{
    private const string GetCampaignByIdRoute = "GetCampaignById";

    /// <summary>
    /// Maps the <c>/api/campaigns</c> group.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The route group.</returns>
    public static RouteGroupBuilder MapCampaignEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/campaigns").WithTags("Campaigns");

        group.MapGet("/", ListAsync)
            .AllowAnonymous();

        group.MapGet("/{campaignId:guid}", GetByIdAsync)
            .AllowAnonymous()
            .WithName(GetCampaignByIdRoute)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateAsync)
            .RequireAuthorization(PermissionConstants.CampaignsCreate)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/{campaignId:guid}/publish", PublishAsync)
            .RequireAuthorization(PermissionConstants.CampaignsPublish)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{campaignId:guid}/cancel", CancelAsync)
            .RequireAuthorization(PermissionConstants.CampaignsCancel)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    /// <summary>
    /// Lists campaigns, newest first, optionally filtered by owner, category and status.
    /// </summary>
    /// <param name="pageNumber">The 1-based page number (default 1).</param>
    /// <param name="pageSize">The page size (default 20).</param>
    /// <param name="ownerId">Only campaigns owned by this user.</param>
    /// <param name="category">Only campaigns in this category.</param>
    /// <param name="status">Only campaigns in this status (Draft, Published, Successful, Failed, Cancelled).</param>
    /// <param name="dispatcher">The query dispatcher.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>One page of campaigns.</returns>
    internal static async Task<Ok<PagedResponse<ListCampaignsResponse>>> ListAsync(
        int? pageNumber,
        int? pageSize,
        Guid? ownerId,
        string? category,
        string? status,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var page = await dispatcher.QueryAsync<PagedResult<ListCampaignsResult>>(
            new ListCampaignsQuery(PageRequest.Create(pageNumber, pageSize), new ListCampaignsFilter(ownerId, category, status)),
            cancellationToken);

        return TypedResults.Ok(PagedResponse.From(page, campaign => new ListCampaignsResponse(
            campaign.Id, campaign.OwnerId, campaign.Title, campaign.Category, campaign.GoalAmount, campaign.GoalCurrency,
            campaign.RaisedAmount, campaign.RaisedCurrency, campaign.DeadlineUtc, campaign.Status, campaign.CreatedAtUtc)));
    }

    /// <summary>
    /// Returns one campaign.
    /// </summary>
    /// <param name="campaignId">The campaign id.</param>
    /// <param name="dispatcher">The query dispatcher.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The campaign.</returns>
    internal static async Task<Ok<GetCampaignByIdResponse>> GetByIdAsync(
        Guid campaignId, IQueryDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var campaign = await dispatcher.QueryAsync<GetCampaignByIdResult>(new GetCampaignByIdQuery(campaignId), cancellationToken);

        return TypedResults.Ok(new GetCampaignByIdResponse(
            campaign.Id, campaign.OwnerId, campaign.Title, campaign.Story, campaign.Category, campaign.GoalAmount, campaign.GoalCurrency,
            campaign.RaisedAmount, campaign.RaisedCurrency, campaign.DeadlineUtc, campaign.Status, campaign.CreatedAtUtc));
    }

    /// <summary>
    /// Creates a draft campaign owned by the caller. It must pass moderation before it can be published.
    /// </summary>
    /// <param name="request">The campaign details.</param>
    /// <param name="dispatcher">The command dispatcher.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The new campaign's id, with a Location header pointing at it.</returns>
    internal static async Task<CreatedAtRoute<CreateCampaignResponse>> CreateAsync(
        CreateCampaignRequest request, ICommandDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync<CreateCampaignResult>(
            new CreateCampaignCommand(request.Title, request.Story, request.Category, request.GoalAmount, request.Currency, request.DeadlineUtc),
            cancellationToken);

        // The Location header is generated from the named GET route, so it cannot drift from it.
        return TypedResults.CreatedAtRoute(
            new CreateCampaignResponse(result.CampaignId), GetCampaignByIdRoute, new { campaignId = result.CampaignId });
    }

    /// <summary>
    /// Publishes an approved draft campaign so it can accept contributions.
    /// </summary>
    /// <param name="campaignId">The campaign id.</param>
    /// <param name="dispatcher">The command dispatcher.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The campaign's new status.</returns>
    internal static async Task<Ok<PublishCampaignResponse>> PublishAsync(
        Guid campaignId, ICommandDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync<PublishCampaignResult>(new PublishCampaignCommand(campaignId), cancellationToken);
        return TypedResults.Ok(new PublishCampaignResponse(result.CampaignId, result.Status));
    }

    /// <summary>
    /// Cancels a campaign; confirmed contributions are refunded asynchronously.
    /// </summary>
    /// <param name="campaignId">The campaign id.</param>
    /// <param name="dispatcher">The command dispatcher.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The campaign's new status.</returns>
    internal static async Task<Ok<CancelCampaignResponse>> CancelAsync(
        Guid campaignId, ICommandDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync<CancelCampaignResult>(new CancelCampaignCommand(campaignId), cancellationToken);
        return TypedResults.Ok(new CancelCampaignResponse(result.CampaignId, result.Status));
    }
}

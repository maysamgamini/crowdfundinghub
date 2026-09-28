using CrowdFunding.API.Contracts.CampaignUpdates;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.CampaignUpdates.Application.Features.WebhookSubscriptions.Commands.DeleteWebhookSubscription;
using CrowdFunding.Modules.CampaignUpdates.Application.Features.WebhookSubscriptions.Commands.RegisterWebhookSubscription;
using CrowdFunding.Modules.CampaignUpdates.Application.Features.WebhookSubscriptions.Queries.GetWebhookSubscriptionById;
using CrowdFunding.Modules.CampaignUpdates.Application.Features.WebhookSubscriptions.Queries.ListWebhookSubscriptionsByCampaign;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CrowdFunding.API.Endpoints;

/// <summary>
/// Outbound webhook subscriptions a campaign owner registers to receive signed campaign events
/// (see CampaignUpdates.Contracts for the payload and signature contract).
/// </summary>
public static class WebhookSubscriptionEndpoints
{
    private const string GetWebhookSubscriptionByIdRoute = "GetWebhookSubscriptionById";

    /// <summary>
    /// Maps the <c>/api/campaigns/{campaignId}/webhook-subscriptions</c> group. Every operation
    /// requires an authenticated caller; the handlers enforce campaign ownership.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The route group.</returns>
    public static RouteGroupBuilder MapWebhookSubscriptionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/campaigns/{campaignId:guid}/webhook-subscriptions")
            .WithTags("CampaignUpdates")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", RegisterAsync)
            .ProducesValidationProblem();

        group.MapGet("/", ListAsync);

        group.MapGet("/{subscriptionId:guid}", GetByIdAsync)
            .WithName(GetWebhookSubscriptionByIdRoute);

        group.MapDelete("/{subscriptionId:guid}", DeleteAsync);

        return group;
    }

    /// <summary>
    /// Registers a public HTTPS endpoint to receive the campaign's events. The response carries the
    /// HMAC signing secret, shown only once.
    /// </summary>
    /// <param name="campaignId">The campaign id.</param>
    /// <param name="request">The target URL.</param>
    /// <param name="dispatcher">The command dispatcher.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The subscription and its secret, with a Location header pointing at it.</returns>
    internal static async Task<CreatedAtRoute<RegisterWebhookSubscriptionResponse>> RegisterAsync(
        Guid campaignId, RegisterWebhookSubscriptionRequest request, ICommandDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync<RegisterWebhookSubscriptionResult>(
            new RegisterWebhookSubscriptionCommand(campaignId, request.TargetUrl), cancellationToken);

        return TypedResults.CreatedAtRoute(
            new RegisterWebhookSubscriptionResponse(result.SubscriptionId, result.CampaignId, result.TargetUrl, result.SecretKey),
            GetWebhookSubscriptionByIdRoute,
            new { campaignId, subscriptionId = result.SubscriptionId });
    }

    /// <summary>
    /// Lists the campaign's webhook subscriptions.
    /// </summary>
    /// <param name="campaignId">The campaign id.</param>
    /// <param name="dispatcher">The query dispatcher.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The subscriptions.</returns>
    internal static async Task<Ok<ListWebhookSubscriptionsResponse>> ListAsync(
        Guid campaignId, IQueryDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.QueryAsync<ListWebhookSubscriptionsByCampaignResult>(
            new ListWebhookSubscriptionsByCampaignQuery(campaignId), cancellationToken);

        return TypedResults.Ok(new ListWebhookSubscriptionsResponse(result.Subscriptions.Select(subscription => new WebhookSubscriptionResponse(
            subscription.SubscriptionId, subscription.CampaignId, subscription.TargetUrl, subscription.IsActive,
            subscription.ConsecutiveFailureCount, subscription.CreatedAtUtc)).ToList()));
    }

    /// <summary>
    /// Returns one webhook subscription.
    /// </summary>
    /// <param name="campaignId">The campaign id.</param>
    /// <param name="subscriptionId">The subscription id.</param>
    /// <param name="dispatcher">The query dispatcher.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The subscription.</returns>
    internal static async Task<Ok<WebhookSubscriptionResponse>> GetByIdAsync(
        Guid campaignId, Guid subscriptionId, IQueryDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var subscription = await dispatcher.QueryAsync<GetWebhookSubscriptionByIdResult>(
            new GetWebhookSubscriptionByIdQuery(campaignId, subscriptionId), cancellationToken);

        return TypedResults.Ok(new WebhookSubscriptionResponse(
            subscription.SubscriptionId, subscription.CampaignId, subscription.TargetUrl, subscription.IsActive,
            subscription.ConsecutiveFailureCount, subscription.CreatedAtUtc));
    }

    /// <summary>
    /// Deletes a webhook subscription.
    /// </summary>
    /// <param name="campaignId">The campaign id.</param>
    /// <param name="subscriptionId">The subscription id.</param>
    /// <param name="dispatcher">The command dispatcher.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>No content.</returns>
    internal static async Task<NoContent> DeleteAsync(
        Guid campaignId, Guid subscriptionId, ICommandDispatcher dispatcher, CancellationToken cancellationToken)
    {
        await dispatcher.SendAsync<DeleteWebhookSubscriptionResult>(
            new DeleteWebhookSubscriptionCommand(campaignId, subscriptionId), cancellationToken);

        return TypedResults.NoContent();
    }
}

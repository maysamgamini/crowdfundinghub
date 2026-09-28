using CrowdFunding.API.Contracts.Notifications;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Notifications.Application.Features.Preferences.Commands.Unsubscribe;
using CrowdFunding.Modules.Notifications.Application.Features.Preferences.Commands.UpdateNotificationPreferences;
using CrowdFunding.Modules.Notifications.Contracts.Queries.GetNotificationPreferences;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CrowdFunding.API.Endpoints;

/// <summary>
/// Notification preference endpoints. The caller is always identified from the access token's
/// <c>sub</c> claim (<see cref="ICurrentUser"/>), never from the request body, except for the
/// anonymous one-click unsubscribe.
/// </summary>
public static class NotificationEndpoints
{
    /// <summary>
    /// Maps the <c>/api/notifications</c> group.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The route group.</returns>
    public static RouteGroupBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications").WithTags("Notifications");

        group.MapGet("/preferences", GetPreferencesAsync)
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPut("/preferences", UpdatePreferencesAsync)
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/unsubscribe", UnsubscribeAsync)
            .AllowAnonymous();

        return group;
    }

    /// <summary>
    /// Returns the caller's notification preferences (defaults when none were saved).
    /// </summary>
    /// <param name="currentUser">The authenticated caller.</param>
    /// <param name="dispatcher">The query dispatcher.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The caller's preferences.</returns>
    internal static async Task<Ok<NotificationPreferencesResponse>> GetPreferencesAsync(
        ICurrentUser currentUser, IQueryDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.QueryAsync<GetNotificationPreferencesResult>(
            new GetNotificationPreferencesQuery(currentUser.UserId), cancellationToken);

        return TypedResults.Ok(new NotificationPreferencesResponse(
            result.UserId, result.CampaignUpdatesEnabled, result.MarketingAnnouncementsEnabled, result.UpdatedAtUtc));
    }

    /// <summary>
    /// Updates the caller's notification preferences.
    /// </summary>
    /// <param name="request">The new opt-ins.</param>
    /// <param name="currentUser">The authenticated caller.</param>
    /// <param name="dispatcher">The command dispatcher.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The updated preferences.</returns>
    internal static async Task<Ok<NotificationPreferencesResponse>> UpdatePreferencesAsync(
        UpdateNotificationPreferencesRequest request, ICurrentUser currentUser, ICommandDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync<UpdateNotificationPreferencesResult>(
            new UpdateNotificationPreferencesCommand(currentUser.UserId, request.CampaignUpdatesEnabled, request.MarketingAnnouncementsEnabled),
            cancellationToken);

        return TypedResults.Ok(new NotificationPreferencesResponse(
            result.UserId, result.CampaignUpdatesEnabled, result.MarketingAnnouncementsEnabled, result.UpdatedAtUtc));
    }

    /// <summary>
    /// One-click unsubscribe from all non-transactional e-mail: the caller's own account when
    /// authenticated, otherwise the user id in the body (the link in an e-mail footer).
    /// </summary>
    /// <remarks>
    /// Known limitation, documented in docs/PLATFORM_SHORTCOMINGS.md: the anonymous path trusts a
    /// bare user id. A production unsubscribe link carries a signed, expiring token instead.
    /// </remarks>
    /// <param name="request">The optional user id for the anonymous path.</param>
    /// <param name="currentUser">The caller, when authenticated.</param>
    /// <param name="dispatcher">The command dispatcher.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The unsubscribed user, or a validation problem when no user can be determined.</returns>
    internal static async Task<Results<Ok<UnsubscribeResponse>, ValidationProblem>> UnsubscribeAsync(
        UnsubscribeRequest? request, ICurrentUser currentUser, ICommandDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var targetUserId = request?.UserId ?? (currentUser.IsAuthenticated ? currentUser.UserId : Guid.Empty);
        if (targetUserId == Guid.Empty)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(UnsubscribeRequest.UserId)] = ["A valid UserId must be provided to unsubscribe."],
            });
        }

        var result = await dispatcher.SendAsync<UnsubscribeResult>(new UnsubscribeCommand(targetUserId), cancellationToken);
        return TypedResults.Ok(new UnsubscribeResponse(result.UserId, result.Unsubscribed));
    }
}

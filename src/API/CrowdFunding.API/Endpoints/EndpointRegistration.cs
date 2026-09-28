namespace CrowdFunding.API.Endpoints;

/// <summary>
/// Maps every Minimal API endpoint group of the host.
/// </summary>
/// <remarks>
/// Endpoints are thin HTTP adapters: bind the request, build a command or query, dispatch it
/// (validation, auditing and the handler run in the command pipeline), and map the result to an
/// HTTP response contract. Errors are exceptions turned into RFC 9457 ProblemDetails by
/// <c>GlobalExceptionHandler</c>, so handlers only describe the success path.
/// <para>
/// Handlers are named <c>internal static</c> methods rather than lambdas: they stay unit-testable,
/// read like a table of contents, and the .NET 10 OpenAPI source generator turns their XML
/// comments into operation summaries (it skips private members).
/// </para>
/// <para>
/// One controller, <c>Controllers/ModerationController</c>, is kept on purpose for side-by-side
/// comparison with <see cref="CampaignEndpoints"/> (see docs/minimal-apis-vs-controllers.md).
/// </para>
/// </remarks>
public static class EndpointRegistration
{
    /// <summary>
    /// Maps the Identity, Campaigns, RewardTiers, Contributions, CampaignUpdates, Notifications and
    /// inbound webhook endpoint groups.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The same endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapCrowdFundingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapIdentityEndpoints();
        app.MapCampaignEndpoints();
        app.MapRewardTierEndpoints();
        app.MapContributionEndpoints();
        app.MapWebhookSubscriptionEndpoints();
        app.MapNotificationEndpoints();
        app.MapInboundWebhookEndpoints();
        return app;
    }
}

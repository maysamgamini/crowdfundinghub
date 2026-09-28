using System.Text.Json;
using CrowdFunding.API.Contracts.Webhooks;
using CrowdFunding.API.RateLimiting;
using CrowdFunding.API.Security;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.ReconcilePaymentWebhook;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.RecordMediaAnalysis;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CrowdFunding.API.Endpoints;

/// <summary>
/// Callbacks from external systems: the payment gateway (TICKET-033) and the media-analysis cloud
/// function (TICKET-032). Anonymous by design (neither can present a user JWT) and authenticated
/// by an HMAC signature over the raw body instead.
/// </summary>
public static class InboundWebhookEndpoints
{
    // Gateways send lowercase JSON keys ("id", "type", "data.object.id").
    private static readonly JsonSerializerOptions PayloadSerializerOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Maps the <c>/api/webhooks</c> group.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The route group.</returns>
    public static RouteGroupBuilder MapInboundWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/webhooks")
            .WithTags("Webhooks")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingConfiguration.WebhookPolicy);

        group.MapPost("/payments/stripe", HandleStripeWebhookAsync)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/moderation/media-analysis", HandleMediaAnalysisAsync)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    /// <summary>
    /// Reconciles a Stripe-style payment event with the contribution its payment intent belongs to.
    /// Redelivered events are acknowledged with <c>Ignored</c> and not applied twice.
    /// </summary>
    /// <param name="signature">The <c>Stripe-Signature</c> header.</param>
    /// <param name="request">The HTTP request; the signature covers its exact body bytes.</param>
    /// <param name="options">The shared secret and timestamp tolerance.</param>
    /// <param name="dispatcher">The command dispatcher.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>200 with the outcome, 400 for a malformed payload, 401 for a bad signature.</returns>
    internal static async Task<Results<Ok<WebhookAcknowledgement>, BadRequest, UnauthorizedHttpResult>> HandleStripeWebhookAsync(
        [FromHeader(Name = "Stripe-Signature")] string? signature,
        HttpRequest request,
        IOptions<PaymentGatewayWebhookOptions> options,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        // The raw body is read directly: the HMAC must be checked over the exact bytes that were
        // signed, which a round trip through JSON model binding does not guarantee.
        var rawBody = await ReadBodyAsync(request, cancellationToken);

        if (!StripeWebhookSignatureVerifier.Verify(
                rawBody, signature, options.Value.Secret, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(options.Value.ToleranceSeconds)))
        {
            return TypedResults.Unauthorized();
        }

        var payload = TryDeserialize<StripeWebhookPayload>(rawBody);
        if (payload is null || string.IsNullOrWhiteSpace(payload.Id) || string.IsNullOrWhiteSpace(payload.Type)
            || string.IsNullOrWhiteSpace(payload.Data?.Object?.Id))
        {
            return TypedResults.BadRequest();
        }

        var result = await dispatcher.SendAsync<ReconcilePaymentWebhookResult>(
            new ReconcilePaymentWebhookCommand(payload.Id, payload.Data.Object.Id, payload.Type), cancellationToken);

        return TypedResults.Ok(new WebhookAcknowledgement(result.Status));
    }

    /// <summary>
    /// Records the outcome of an asynchronous media-safety analysis for a campaign's review.
    /// </summary>
    /// <param name="signature">The <c>X-Cloud-Signature</c> header.</param>
    /// <param name="request">The HTTP request; the signature covers its exact body bytes.</param>
    /// <param name="options">The shared secret.</param>
    /// <param name="dispatcher">The command dispatcher.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>200 with the review status, 400 for a malformed payload, 401 for a bad signature.</returns>
    internal static async Task<Results<Ok<WebhookAcknowledgement>, BadRequest, UnauthorizedHttpResult>> HandleMediaAnalysisAsync(
        [FromHeader(Name = "X-Cloud-Signature")] string? signature,
        HttpRequest request,
        IOptions<CloudFunctionWebhookOptions> options,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var rawBody = await ReadBodyAsync(request, cancellationToken);

        if (!CloudFunctionSignatureVerifier.Verify(rawBody, signature, options.Value.Secret))
        {
            return TypedResults.Unauthorized();
        }

        var payload = TryDeserialize<MediaAnalysisResultWebhook>(rawBody);
        if (payload is null || payload.CampaignId == Guid.Empty)
        {
            return TypedResults.BadRequest();
        }

        var result = await dispatcher.SendAsync<RecordMediaAnalysisResult>(
            new RecordMediaAnalysisCommand(payload.CampaignId, payload.PassedSafetyCheck, payload.ToxicityScore, payload.AdultContentScore),
            cancellationToken);

        return TypedResults.Ok(new WebhookAcknowledgement(result.Status));
    }

    internal static async Task<string> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static T? TryDeserialize<T>(string rawBody)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(rawBody, PayloadSerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

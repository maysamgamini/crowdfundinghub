using System.Text.Json;
using CrowdFunding.API.Contracts.Webhooks;
using CrowdFunding.API.RateLimiting;
using CrowdFunding.API.Security;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.ReconcilePaymentWebhook;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace CrowdFunding.API.Controllers;

/// <summary>
/// Receives asynchronous payment-gateway callbacks. Deliberately anonymous — a payment gateway
/// cannot present a user JWT — and deliberately outside <c>api/campaigns/{campaignId}/contributions</c>:
/// a webhook is not a resource under a specific campaign, it is a top-level integration surface
/// authenticated by cryptographic signature instead of a bearer token. See TICKET-033.
/// </summary>
[ApiController]
[Route("api/webhooks/payments")]
[Tags("Webhooks")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitingConfiguration.WebhookPolicy)]
public sealed class PaymentWebhooksController : ControllerBase
{
    // Stripe's real webhook payloads use lowercase JSON keys ("id", "type", "data.object.id");
    // the default JsonSerializerOptions used elsewhere in this API is case-sensitive, which would
    // silently fail to populate this record's PascalCase properties instead of throwing.
    private static readonly JsonSerializerOptions PayloadSerializerOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ICommandDispatcher _commandDispatcher;
    private readonly PaymentGatewayWebhookOptions _options;

    public PaymentWebhooksController(ICommandDispatcher commandDispatcher, IOptions<PaymentGatewayWebhookOptions> options)
    {
        _commandDispatcher = commandDispatcher;
        _options = options.Value;
    }

    /// <summary>
    /// Reconciles a Stripe-style payment webhook against the contribution its payment intent
    /// correlates to.
    /// </summary>
    /// <response code="200">Webhook acknowledged — processed, ignored, or a duplicate delivery.</response>
    /// <response code="400">Malformed payload.</response>
    /// <response code="401">Missing or invalid <c>Stripe-Signature</c> header.</response>
    /// <response code="404">No contribution correlates to the payload's payment intent.</response>
    [HttpPost("stripe")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HandleStripeWebhook(
        [FromHeader(Name = "Stripe-Signature")] string? signature,
        CancellationToken cancellationToken)
    {
        // Read the raw body ourselves rather than relying on [FromBody] model binding: the HMAC
        // must be computed over the exact bytes Stripe signed, which a round-trip through the
        // model binder's own JSON reader does not guarantee byte-for-byte.
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);

        if (!StripeWebhookSignatureVerifier.Verify(
                rawBody, signature, _options.Secret, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(_options.ToleranceSeconds)))
        {
            return Unauthorized();
        }

        StripeWebhookPayload payload;
        try
        {
            payload = JsonSerializer.Deserialize<StripeWebhookPayload>(rawBody, PayloadSerializerOptions)
                ?? throw new JsonException("Webhook payload deserialized to null.");

            if (string.IsNullOrWhiteSpace(payload.Id) || string.IsNullOrWhiteSpace(payload.Type)
                || string.IsNullOrWhiteSpace(payload.Data?.Object?.Id))
            {
                throw new JsonException("Webhook payload is missing required fields.");
            }
        }
        catch (JsonException)
        {
            return BadRequest();
        }

        var result = await _commandDispatcher.SendAsync<ReconcilePaymentWebhookResult>(
            new ReconcilePaymentWebhookCommand(payload.Id, payload.Data.Object.Id, payload.Type),
            cancellationToken);

        return Ok(new { status = result.Status });
    }
}

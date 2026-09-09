using System.Text.Json;
using CrowdFunding.API.Contracts.Webhooks;
using CrowdFunding.API.RateLimiting;
using CrowdFunding.API.Security;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.RecordMediaAnalysis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace CrowdFunding.API.Controllers;

/// <summary>
/// Receives asynchronous callbacks from the serverless Cloud Function that analyzes campaign
/// media for safety/toxicity. Deliberately anonymous — a Cloud Function can't present a user
/// JWT — authenticated instead by an HMAC-signed <c>X-Cloud-Signature</c> header. See TICKET-032.
/// </summary>
[ApiController]
[Route("api/webhooks/moderation")]
[Tags("Webhooks")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitingConfiguration.WebhookPolicy)]
public sealed class ModerationWebhooksController : ControllerBase
{
    private static readonly JsonSerializerOptions PayloadSerializerOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ICommandDispatcher _commandDispatcher;
    private readonly CloudFunctionWebhookOptions _options;

    public ModerationWebhooksController(ICommandDispatcher commandDispatcher, IOptions<CloudFunctionWebhookOptions> options)
    {
        _commandDispatcher = commandDispatcher;
        _options = options.Value;
    }

    /// <summary>
    /// Records the outcome of an asynchronous media-safety analysis for a campaign.
    /// </summary>
    /// <response code="200">Acknowledged and recorded.</response>
    /// <response code="400">Malformed payload.</response>
    /// <response code="401">Missing or invalid <c>X-Cloud-Signature</c> header.</response>
    /// <response code="404">No campaign review exists for the payload's campaign id.</response>
    [HttpPost("media-analysis")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HandleMediaAnalysis(
        [FromHeader(Name = "X-Cloud-Signature")] string? signature,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);

        if (!CloudFunctionSignatureVerifier.Verify(rawBody, signature, _options.Secret))
        {
            return Unauthorized();
        }

        MediaAnalysisResultWebhook payload;
        try
        {
            payload = JsonSerializer.Deserialize<MediaAnalysisResultWebhook>(rawBody, PayloadSerializerOptions)
                ?? throw new JsonException("Webhook payload deserialized to null.");

            if (payload.CampaignId == Guid.Empty)
            {
                throw new JsonException("Webhook payload is missing required fields.");
            }
        }
        catch (JsonException)
        {
            return BadRequest();
        }

        var result = await _commandDispatcher.SendAsync<RecordMediaAnalysisResult>(
            new RecordMediaAnalysisCommand(payload.CampaignId, payload.PassedSafetyCheck, payload.ToxicityScore, payload.AdultContentScore),
            cancellationToken);

        return Ok(new { status = result.Status });
    }
}

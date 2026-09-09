using System.Security.Cryptography;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.CampaignUpdates.Application.Abstractions.Persistence;
using CrowdFunding.Modules.CampaignUpdates.Application.Abstractions.Services;
using CrowdFunding.Modules.CampaignUpdates.Application.Security;
using CrowdFunding.Modules.CampaignUpdates.Domain.Aggregates;

namespace CrowdFunding.Modules.CampaignUpdates.Application.Features.WebhookSubscriptions.Commands.RegisterWebhookSubscription;

/// <summary>
/// Registers a creator's outbound webhook subscription for their campaign's pledge activity.
/// See TICKET-035.
/// </summary>
public sealed class RegisterWebhookSubscriptionCommandHandler
    : ICommandHandler<RegisterWebhookSubscriptionCommand, RegisterWebhookSubscriptionResult>
{
    private readonly ICampaignOwnerCacheRepository _campaignOwnerCacheRepository;
    private readonly IWebhookSubscriptionRepository _webhookSubscriptionRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ICampaignUpdatesDateTimeProvider _dateTimeProvider;

    public RegisterWebhookSubscriptionCommandHandler(
        ICampaignOwnerCacheRepository campaignOwnerCacheRepository,
        IWebhookSubscriptionRepository webhookSubscriptionRepository,
        ICurrentUser currentUser,
        ICampaignUpdatesDateTimeProvider dateTimeProvider)
    {
        _campaignOwnerCacheRepository = campaignOwnerCacheRepository;
        _webhookSubscriptionRepository = webhookSubscriptionRepository;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<RegisterWebhookSubscriptionResult> Handle(RegisterWebhookSubscriptionCommand command, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("The current user must be authenticated to register a webhook subscription.");
        }

        // Reads CampaignUpdates' own locally replicated campaign-owner snapshot rather than
        // calling back into Campaigns synchronously (TICKET-023 pattern).
        var campaign = await _campaignOwnerCacheRepository.GetAsync(command.CampaignId, cancellationToken);
        if (campaign is null)
        {
            throw new KeyNotFoundException($"Campaign with id '{command.CampaignId}' was not found.");
        }

        if (campaign.OwnerId != _currentUser.UserId)
        {
            throw new ForbiddenAccessException("Only a campaign's owner may register webhook subscriptions for it.");
        }

        if (!Uri.TryCreate(command.TargetUrl, UriKind.Absolute, out var targetUri) || !UrlSecurityValidator.IsSafeExternalUrl(targetUri))
        {
            throw new ArgumentException(
                "Target URL must be a public HTTPS address — private, loopback, and link-local addresses are not allowed.",
                nameof(command.TargetUrl));
        }

        var secretKey = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var subscription = WebhookSubscription.Create(command.CampaignId, command.TargetUrl, secretKey, _dateTimeProvider.UtcNow);

        await _webhookSubscriptionRepository.AddAsync(subscription, cancellationToken);

        return new RegisterWebhookSubscriptionResult(subscription.Id, subscription.CampaignId, subscription.TargetUrl, secretKey);
    }
}

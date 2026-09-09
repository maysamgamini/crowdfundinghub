using CrowdFunding.API.Contracts.CampaignUpdates;
using CrowdFunding.Modules.CampaignUpdates.Application.Features.WebhookSubscriptions.Commands.RegisterWebhookSubscription;
using Mapster;

namespace CrowdFunding.API.Mapping;

/// <summary>
/// Registers Mapster mappings for CampaignUpdates.
/// </summary>
public static class CampaignUpdatesMappingConfig
{
    public static void Register(TypeAdapterConfig config)
    {
        config.NewConfig<RegisterWebhookSubscriptionResult, RegisterWebhookSubscriptionResponse>();
    }
}

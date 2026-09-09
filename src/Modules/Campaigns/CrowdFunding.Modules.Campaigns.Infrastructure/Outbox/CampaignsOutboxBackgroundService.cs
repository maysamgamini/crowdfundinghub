using CrowdFunding.BuildingBlocks.Infrastructure.Outbox;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Outbox;

/// <summary>
/// The Campaigns module's autonomous outbox worker. See <see cref="ModuleOutboxProcessor{TDbContext}"/>.
/// </summary>
public sealed class CampaignsOutboxBackgroundService : ModuleOutboxProcessor<CampaignsDbContext>
{
    public CampaignsOutboxBackgroundService(IServiceProvider serviceProvider)
        : base(serviceProvider, "campaigns_outbox_messages", TimeSpan.FromSeconds(5))
    {
    }
}

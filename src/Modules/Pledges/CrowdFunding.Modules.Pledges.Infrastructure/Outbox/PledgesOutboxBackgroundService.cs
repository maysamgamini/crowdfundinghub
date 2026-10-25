using CrowdFunding.BuildingBlocks.Infrastructure.Outbox;
using CrowdFunding.Modules.Pledges.Infrastructure.Persistence.DbContexts;

namespace CrowdFunding.Modules.Pledges.Infrastructure.Outbox;

/// <summary>
/// The Pledges module's autonomous outbox worker. See <see cref="ModuleOutboxProcessor{TDbContext}"/>.
/// </summary>
public sealed class PledgesOutboxBackgroundService : ModuleOutboxProcessor<PledgesDbContext>
{
    public PledgesOutboxBackgroundService(IServiceProvider serviceProvider)
        : base(serviceProvider, "pledges_outbox_messages", TimeSpan.FromSeconds(5))
    {
    }
}

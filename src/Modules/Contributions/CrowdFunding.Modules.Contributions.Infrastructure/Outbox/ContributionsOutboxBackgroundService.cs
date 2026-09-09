using CrowdFunding.BuildingBlocks.Infrastructure.Outbox;
using CrowdFunding.Modules.Contributions.Infrastructure.Persistence.DbContexts;

namespace CrowdFunding.Modules.Contributions.Infrastructure.Outbox;

/// <summary>
/// The Contributions module's autonomous outbox worker. See <see cref="ModuleOutboxProcessor{TDbContext}"/>.
/// </summary>
public sealed class ContributionsOutboxBackgroundService : ModuleOutboxProcessor<ContributionsDbContext>
{
    public ContributionsOutboxBackgroundService(IServiceProvider serviceProvider)
        : base(serviceProvider, "contributions_outbox_messages", TimeSpan.FromSeconds(5))
    {
    }
}

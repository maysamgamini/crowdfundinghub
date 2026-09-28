using CrowdFunding.BuildingBlocks.Infrastructure.Outbox;
using CrowdFunding.Modules.Moderation.Infrastructure.Persistence.DbContexts;

namespace CrowdFunding.Modules.Moderation.Infrastructure.Outbox;

/// <summary>
/// The Moderation module's autonomous outbox worker. See <see cref="ModuleOutboxProcessor{TDbContext}"/>.
/// </summary>
public sealed class ModerationOutboxBackgroundService : ModuleOutboxProcessor<ModerationDbContext>
{
    public ModerationOutboxBackgroundService(IServiceProvider serviceProvider)
        : base(serviceProvider, TimeSpan.FromSeconds(5))
    {
    }
}

using CrowdFunding.BuildingBlocks.Infrastructure.Inbox;
using CrowdFunding.Modules.CampaignUpdates.Application.Abstractions.Inbox;
using CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.DbContexts;

namespace CrowdFunding.Modules.CampaignUpdates.Infrastructure.Inbox;

/// <summary>
/// <see cref="ICampaignUpdatesIdempotentConsumer"/> over <see cref="CampaignUpdatesDbContext"/>.
/// </summary>
public sealed class CampaignUpdatesIdempotentConsumer : EfIdempotentConsumer<CampaignUpdatesDbContext>, ICampaignUpdatesIdempotentConsumer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CampaignUpdatesIdempotentConsumer"/> class.
    /// </summary>
    /// <param name="dbContext">The CampaignUpdates DbContext.</param>
    public CampaignUpdatesIdempotentConsumer(CampaignUpdatesDbContext dbContext)
        : base(dbContext)
    {
    }
}

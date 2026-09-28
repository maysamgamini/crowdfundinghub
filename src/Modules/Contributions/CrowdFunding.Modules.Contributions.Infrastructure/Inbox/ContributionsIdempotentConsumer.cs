using CrowdFunding.BuildingBlocks.Infrastructure.Inbox;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Inbox;
using CrowdFunding.Modules.Contributions.Infrastructure.Persistence.DbContexts;

namespace CrowdFunding.Modules.Contributions.Infrastructure.Inbox;

/// <summary>
/// <see cref="IContributionsIdempotentConsumer"/> over <see cref="ContributionsDbContext"/>.
/// </summary>
public sealed class ContributionsIdempotentConsumer : EfIdempotentConsumer<ContributionsDbContext>, IContributionsIdempotentConsumer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContributionsIdempotentConsumer"/> class.
    /// </summary>
    /// <param name="dbContext">The Contributions DbContext.</param>
    public ContributionsIdempotentConsumer(ContributionsDbContext dbContext)
        : base(dbContext)
    {
    }
}

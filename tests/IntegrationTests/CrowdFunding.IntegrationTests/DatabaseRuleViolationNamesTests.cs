using CrowdFunding.API.Observability;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Pledges.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// The sentences in <see cref="DatabaseRuleViolation"/> are looked up by constraint name. A name that drifts from the
/// model (a rename, a typo) would silently fall back to the generic sentence, so every name must exist.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class DatabaseRuleViolationNamesTests
{
    private readonly CrowdFundingApiFactory _factory;

    public DatabaseRuleViolationNamesTests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void EveryConstraintNameWithItsOwnSentence_ExistsInAModuleModel()
    {
        using var scope = _factory.Services.CreateScope();
        DbContext[] contexts =
        [
            scope.ServiceProvider.GetRequiredService<CampaignsDbContext>(),
            scope.ServiceProvider.GetRequiredService<PledgesDbContext>(),
        ];

        var names = contexts
            .SelectMany(db => db.GetService<IDesignTimeModel>().Model.GetEntityTypes())
            .SelectMany(entity => entity.GetCheckConstraints().Select(c => c.Name!)
                .Concat(entity.GetIndexes().Select(i => i.GetDatabaseName()!)))
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(DatabaseRuleViolation.KnownConstraintNames, name => Assert.Contains(name, names));
    }
}

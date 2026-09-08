namespace CrowdFunding.IntegrationTests;

/// <summary>
/// Shares one <see cref="CrowdFundingApiFactory"/> (and its Postgres/Redis containers) across every
/// E2E test class instead of starting a fresh pair of containers per class.
/// </summary>
[CollectionDefinition(Name)]
public sealed class CrowdFundingApiCollection : ICollectionFixture<CrowdFundingApiFactory>
{
    public const string Name = "CrowdFunding API";
}

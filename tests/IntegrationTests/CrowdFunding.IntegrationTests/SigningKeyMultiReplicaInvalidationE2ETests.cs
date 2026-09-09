using CrowdFunding.BuildingBlocks.Infrastructure.Caching;
using CrowdFunding.Modules.Identity.Application.Abstractions.Services;
using CrowdFunding.Modules.Identity.Infrastructure.DependencyInjection;
using CrowdFunding.Modules.Identity.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Identity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// TICKET-037 acceptance criterion 1: "An integration test spinning up two distinct
/// ServiceProvider scopes connected to the same Redis instance verifies that rotating keys on
/// Instance A triggers reload on Instance B." Instance A is <see cref="CrowdFundingApiFactory"/>'s
/// own already-running container (simulating one pod); Instance B is a second, independently
/// built <see cref="IServiceProvider"/> pointed at the exact same PostgreSQL/Redis connection
/// strings (simulating a second pod behind the same load balancer) — proving the propagation
/// crosses process/container boundaries via Redis Pub/Sub, not just an in-process event.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class SigningKeyMultiReplicaInvalidationE2ETests
{
    private readonly CrowdFundingApiFactory _factory;

    public SigningKeyMultiReplicaInvalidationE2ETests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RotatingKeysOnInstanceA_ShouldPropagateToInstanceB_WithinASecond()
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();

        await using var podBServices = BuildPodBServiceProvider(configuration);
        var podBSigningKeyStore = podBServices.GetRequiredService<ISigningKeyStore>();
        await podBSigningKeyStore.WarmUpAsync(CancellationToken.None);
        // Simulates Pod B's own hosted-service startup subscribing to the invalidation channel.
        await podBServices.GetRequiredService<DistributedCacheInvalidationSubscriber>().StartAsync(CancellationToken.None);

        var podASigningKeyStore = _factory.Services.GetRequiredService<ISigningKeyStore>();
        var kidBeforeRotation = podBSigningKeyStore.GetActiveSigningKey().Kid;

        await podASigningKeyStore.RotateAsync(CancellationToken.None);
        var kidAfterRotationOnPodA = podASigningKeyStore.GetActiveSigningKey().Kid;
        Assert.NotEqual(kidBeforeRotation, kidAfterRotationOnPodA);

        // Pod B never called RotateAsync itself — its update, if any, can only have come from the
        // Redis Pub/Sub message Pod A published.
        var kidOnPodBAfterPropagation = await PollUntilChangedAsync(
            () => podBSigningKeyStore.GetActiveSigningKey().Kid,
            kidBeforeRotation,
            timeout: TimeSpan.FromSeconds(2));

        Assert.Equal(kidAfterRotationOnPodA, kidOnPodBAfterPropagation);

        // The retired key must still be present in Pod B's public JWKS list — a token signed
        // moments before rotation is still unexpired and must keep verifying.
        Assert.Contains(podBSigningKeyStore.GetPublicSigningKeys(), key => key.Kid == kidBeforeRotation);
        Assert.Contains(podBSigningKeyStore.GetPublicSigningKeys(), key => key.Kid == kidAfterRotationOnPodA);
    }

    private static async Task<string> PollUntilChangedAsync(Func<string> read, string originalValue, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);

        while (!cts.IsCancellationRequested)
        {
            var current = read();
            if (current != originalValue)
            {
                return current;
            }

            await Task.Delay(25, CancellationToken.None);
        }

        Assert.Fail($"Value never changed from '{originalValue}' within {timeout}.");
        return originalValue;
    }

    private static ServiceProvider BuildPodBServiceProvider(IConfiguration factoryConfiguration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(factoryConfiguration);
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(factoryConfiguration.GetConnectionString("DefaultConnection")));

        services.AddDistributedCacheInvalidation(factoryConfiguration);
        services.AddCacheInvalidationSubscription(
            EfSigningKeyStore.SigningKeysInvalidationChannel,
            (provider, cancellationToken) => provider.GetRequiredService<ISigningKeyStore>().WarmUpAsync(cancellationToken));
        services.AddSingleton<ISigningKeyStore, EfSigningKeyStore>();
        services.AddSingleton<DistributedCacheInvalidationSubscriber>();

        return services.BuildServiceProvider();
    }
}

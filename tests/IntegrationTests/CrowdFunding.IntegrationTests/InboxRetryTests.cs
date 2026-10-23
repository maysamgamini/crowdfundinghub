using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.BuildingBlocks.Infrastructure.Events;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// Verifies the inbox against a real PostgreSQL instance: when one handler of an event succeeds
/// and another throws, redelivering the same outbox message must not run the first handler again.
/// </summary>
[Collection(nameof(CampaignsPostgresCollection))]
public sealed class InboxRetryTests
{
    private readonly CampaignsPostgresFixture _fixture;

    public InboxRetryTests(CampaignsPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RetriedMessage_SkipsHandlersThatAlreadySucceeded()
    {
        var probe = new Probe { FailSecondHandlerOnce = true };
        await using var provider = BuildProvider(probe);
        var messageId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() => PublishAsync(provider, messageId));
        await PublishAsync(provider, messageId);

        Assert.Equal(1, probe.FirstHandlerCalls);
        Assert.Equal(2, probe.SecondHandlerCalls);
    }

    [Fact]
    public async Task SameMessageDeliveredTwice_RunsEachHandlerOnce()
    {
        var probe = new Probe();
        await using var provider = BuildProvider(probe);
        var messageId = Guid.NewGuid();

        await PublishAsync(provider, messageId);
        await PublishAsync(provider, messageId);

        Assert.Equal(1, probe.FirstHandlerCalls);
        Assert.Equal(1, probe.SecondHandlerCalls);
    }

    [Fact]
    public async Task EventPublishedOutsideAnOutboxDispatch_BypassesTheInbox()
    {
        var probe = new Probe();
        await using var provider = BuildProvider(probe);

        await PublishAsync(provider, messageId: null);
        await PublishAsync(provider, messageId: null);

        Assert.Equal(2, probe.FirstHandlerCalls);
    }

    private ServiceProvider BuildProvider(Probe probe)
    {
        var services = new ServiceCollection();
        services.AddSingleton(probe);
        services.AddDbContext<CampaignsDbContext>(o => o.UseNpgsql(_fixture.ConnectionString));
        services.AddScoped<IEventPublisher, ServiceProviderEventPublisher>();
        services.AddScoped<InboxMessageContext>();
        services.AddScoped<IEventHandler<InboxProbeEvent>, FirstHandler>();
        services.AddScoped<IEventHandler<InboxProbeEvent>, SecondHandler>();
        services.AddInbox<CampaignsDbContext>(typeof(InboxRetryTests).Assembly);
        return services.BuildServiceProvider();
    }

    private static async Task PublishAsync(ServiceProvider provider, Guid? messageId)
    {
        await using var scope = provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<InboxMessageContext>().MessageId = messageId;
        await scope.ServiceProvider.GetRequiredService<IEventPublisher>()
            .PublishAsync(new InboxProbeEvent(), CancellationToken.None);
    }

    public sealed class InboxProbeEvent : BaseApplicationEvent
    {
    }

    private sealed class Probe
    {
        public bool FailSecondHandlerOnce { get; set; }
        public int FirstHandlerCalls { get; set; }
        public int SecondHandlerCalls { get; set; }
    }

    private sealed class FirstHandler(Probe probe) : IEventHandler<InboxProbeEvent>
    {
        public Task Handle(InboxProbeEvent notification, CancellationToken cancellationToken)
        {
            probe.FirstHandlerCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class SecondHandler(Probe probe) : IEventHandler<InboxProbeEvent>
    {
        public Task Handle(InboxProbeEvent notification, CancellationToken cancellationToken)
        {
            probe.SecondHandlerCalls++;
            if (probe.FailSecondHandlerOnce && probe.SecondHandlerCalls == 1)
            {
                throw new InvalidOperationException("simulated handler failure");
            }

            return Task.CompletedTask;
        }
    }
}

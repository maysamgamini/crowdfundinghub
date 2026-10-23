using System.Collections.Concurrent;
using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Infrastructure.Events;
using CrowdFunding.BuildingBlocks.Infrastructure.Messaging;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCreated;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// Publishing events to a broker is only half of a distributed design; without a consumer no handler
/// ever runs. These tests run the real publisher and the real consumer host against a RabbitMQ
/// container, with no database: they prove delivery, the message id the inbox relies on, how unknown
/// events are treated, and what happens to a message whose handler keeps failing.
/// </summary>
public sealed class RabbitMqConsumerHostTests : IAsyncLifetime
{
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder()
        .WithImage("rabbitmq:3.13-management-alpine")
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    public Task InitializeAsync() => _rabbitMq.StartAsync();

    public Task DisposeAsync() => _rabbitMq.DisposeAsync().AsTask();

    private sealed class SomeoneElsesEvent(string note) : BaseApplicationEvent
    {
        public string Note { get; } = note;
    }

    private sealed class Recorder
    {
        public ConcurrentQueue<(Guid CampaignId, Guid? MessageId)> Handled { get; } = new();

        public int Failures;

        public int FailuresToInject;
    }

    private sealed class CreatedHandler(Recorder recorder, InboxMessageContext context) : IEventHandler<CampaignCreatedApplicationEvent>
    {
        public Task Handle(CampaignCreatedApplicationEvent notification, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref recorder.Failures) <= recorder.FailuresToInject)
            {
                throw new InvalidOperationException("injected handler failure");
            }

            recorder.Handled.Enqueue((notification.CampaignId, context.MessageId));
            return Task.CompletedTask;
        }
    }

    private sealed record Rig(ServiceProvider Services, RabbitMqConsumerHost Host, RabbitMqMessageBus Bus, Recorder Recorder, string Queue) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Host.StopAsync(CancellationToken.None);
            await Bus.DisposeAsync();
            await Services.DisposeAsync();
        }
    }

    private async Task<Rig> StartAsync(int failuresToInject = 0, int handlerAttempts = 3)
    {
        var uri = new Uri(_rabbitMq.GetConnectionString());
        var queue = $"test-{Guid.NewGuid():N}";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Messaging:Provider"] = "RabbitMQ",
            ["Messaging:RabbitMq:Host"] = uri.Host,
            ["Messaging:RabbitMq:Port"] = uri.Port.ToString(),
            ["Messaging:RabbitMq:Exchange"] = "consumer-tests.events",
            ["Messaging:RabbitMq:Queue"] = queue,
            ["Messaging:RabbitMq:HandlerAttempts"] = handlerAttempts.ToString(),
            ["Messaging:RabbitMq:RetryDelay"] = "00:00:00.050",
        }).Build();

        var recorder = new Recorder { FailuresToInject = failuresToInject };
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(recorder)
            .AddEventTypeRegistry(typeof(CampaignCreatedApplicationEvent).Assembly)
            .AddScoped<IEventHandler<CampaignCreatedApplicationEvent>, CreatedHandler>()
            .AddCrowdFundingMessaging(configuration)
            .BuildServiceProvider();

        var host = services.GetServices<IHostedService>().OfType<RabbitMqConsumerHost>().Single();
        await host.StartAsync(CancellationToken.None);
        await WaitUntilAsync(async () => await QueueExistsAsync(queue));

        return new Rig(services, host, services.GetRequiredService<RabbitMqMessageBus>(), recorder, queue);
    }

    private async Task<bool> QueueExistsAsync(string queue)
    {
        try
        {
            var factory = new ConnectionFactory { Uri = new Uri(_rabbitMq.GetConnectionString()) };
            await using var connection = await factory.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();
            await channel.QueueDeclarePassiveAsync(queue);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<uint> MessageCountAsync(string queue)
    {
        var factory = new ConnectionFactory { Uri = new Uri(_rabbitMq.GetConnectionString()) };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        return (await channel.QueueDeclarePassiveAsync(queue)).MessageCount;
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, int seconds = 15)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("The condition was not met in time.");
    }

    private static CampaignCreatedApplicationEvent Created(Guid campaignId)
        => new(campaignId, Guid.NewGuid(), "Consumed Campaign", "USD", DateTime.UtcNow.AddDays(30));

    [Fact]
    public async Task APublishedEvent_ReachesItsHandler_WithTheOutboxRowIdAsTheMessageId()
    {
        await using var rig = await StartAsync();
        var campaignId = Guid.NewGuid();
        var outboxRowId = Guid.NewGuid();

        await rig.Bus.PublishAsync(Created(campaignId), outboxRowId);
        await WaitUntilAsync(() => Task.FromResult(!rig.Recorder.Handled.IsEmpty));

        var handled = Assert.Single(rig.Recorder.Handled);
        Assert.Equal(campaignId, handled.CampaignId);
        Assert.Equal(outboxRowId, handled.MessageId);
    }

    [Fact]
    public async Task AnEventThisProcessDoesNotKnow_IsAcknowledgedAndIgnored()
    {
        await using var rig = await StartAsync();

        // Not in this process's registry: another service's event.
        await rig.Bus.PublishAsync(new SomeoneElsesEvent("not ours"));
        await WaitUntilAsync(async () => await MessageCountAsync(rig.Queue) == 0);

        Assert.Empty(rig.Recorder.Handled);
        Assert.Equal(0u, await MessageCountAsync($"{rig.Queue}.dead"));
    }

    [Fact]
    public async Task AHandlerThatFailsOnce_IsRetriedAndTheMessageIsStillDelivered()
    {
        await using var rig = await StartAsync(failuresToInject: 1);

        await rig.Bus.PublishAsync(Created(Guid.NewGuid()));
        await WaitUntilAsync(() => Task.FromResult(!rig.Recorder.Handled.IsEmpty));

        Assert.Single(rig.Recorder.Handled);
        Assert.Equal(0u, await MessageCountAsync($"{rig.Queue}.dead"));
    }

    [Fact]
    public async Task AHandlerThatAlwaysFails_SendsTheMessageToTheDeadLetterQueue_AfterItsAttempts()
    {
        await using var rig = await StartAsync(failuresToInject: int.MaxValue, handlerAttempts: 3);

        await rig.Bus.PublishAsync(Created(Guid.NewGuid()));
        await WaitUntilAsync(async () => await MessageCountAsync($"{rig.Queue}.dead") == 1);

        Assert.Empty(rig.Recorder.Handled);
        Assert.Equal(3, rig.Recorder.Failures);
        Assert.Equal(0u, await MessageCountAsync(rig.Queue));
    }

    [Fact]
    public async Task AMessageThatIsNotAnEnvelope_GoesToTheDeadLetterQueue()
    {
        await using var rig = await StartAsync();

        var factory = new ConnectionFactory { Uri = new Uri(_rabbitMq.GetConnectionString()) };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await channel.BasicPublishAsync("consumer-tests.events", "garbage", mandatory: false,
            basicProperties: new BasicProperties(), body: "this is not json"u8.ToArray());

        await WaitUntilAsync(async () => await MessageCountAsync($"{rig.Queue}.dead") == 1);
        Assert.Empty(rig.Recorder.Handled);
    }
}

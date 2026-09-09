using System.Text;
using System.Text.Json;
using CrowdFunding.BuildingBlocks.Infrastructure.Messaging;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCreated;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Testcontainers.RabbitMq;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// Proves TICKET-024's <see cref="RabbitMqMessageBus"/> actually delivers CloudEvents v1.0
/// envelopes across a process boundary: a raw <c>RabbitMQ.Client</c> consumer here plays the role
/// of an independently deployed downstream service subscribing to the monolith's events, with no
/// shared code beyond the wire format itself.
/// </summary>
public sealed class RabbitMqMessageBusTests : IAsyncLifetime
{
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder()
        .WithImage("rabbitmq:3.13-management-alpine")
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    public Task InitializeAsync() => _rabbitMq.StartAsync();

    public Task DisposeAsync() => _rabbitMq.DisposeAsync().AsTask();

    [Fact]
    public async Task PublishAsync_ShouldDeliverACloudEventEnvelope_ToAnIndependentSubscriber()
    {
        var uri = new Uri(_rabbitMq.GetConnectionString());
        var options = Options.Create(new RabbitMqOptions
        {
            Host = uri.Host,
            Port = uri.Port,
            Username = "guest",
            Password = "guest",
            Exchange = "crowdfunding.events",
        });

        await using var bus = new RabbitMqMessageBus(options);

        // Set up an independent consumer first — playing the role of a separately deployed
        // downstream service — bound to the same topic exchange the bus publishes to.
        var factory = new ConnectionFactory { Uri = uri };
        await using var consumerConnection = await factory.CreateConnectionAsync();
        await using var consumerChannel = await consumerConnection.CreateChannelAsync();
        await consumerChannel.ExchangeDeclareAsync("crowdfunding.events", ExchangeType.Topic, durable: true);
        var queue = await consumerChannel.QueueDeclareAsync(exclusive: true);
        await consumerChannel.QueueBindAsync(queue.QueueName, "crowdfunding.events", routingKey: "#");

        var messageReceived = new TaskCompletionSource<(IReadOnlyBasicProperties Properties, byte[] Body)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var consumer = new AsyncEventingBasicConsumer(consumerChannel);
        consumer.ReceivedAsync += (_, ea) =>
        {
            messageReceived.TrySetResult((ea.BasicProperties, ea.Body.ToArray()));
            return Task.CompletedTask;
        };
        await consumerChannel.BasicConsumeAsync(queue.QueueName, autoAck: true, consumer);

        var campaignId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        await bus.PublishAsync(new CampaignCreatedApplicationEvent(campaignId, ownerId));

        var received = await messageReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(CloudEventEnvelope.SpecVersion, received.Properties.Headers!["ce-specversion"] switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            var other => other?.ToString(),
        });
        Assert.Equal(
            nameof(CampaignCreatedApplicationEvent),
            received.Properties.Headers["ce-type"] switch { byte[] bytes => Encoding.UTF8.GetString(bytes), var other => other?.ToString() });
        Assert.NotNull(received.Properties.Headers["ce-id"]);
        Assert.Equal(nameof(CampaignCreatedApplicationEvent), received.Properties.Type);

        using var document = JsonDocument.Parse(received.Body);
        var data = document.RootElement.GetProperty("data");
        Assert.Equal(campaignId, data.GetProperty("campaignId").GetGuid());
        Assert.Equal(ownerId, data.GetProperty("ownerId").GetGuid());
    }
}

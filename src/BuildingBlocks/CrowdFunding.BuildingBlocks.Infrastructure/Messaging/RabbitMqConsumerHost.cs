using System.Diagnostics;
using System.Text.Json;
using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.BuildingBlocks.Infrastructure.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Messaging;

/// <summary>
/// The receiving half of <see cref="RabbitMqMessageBus"/>. Publishing events to a broker is only half of
/// a distributed design: without a consumer nothing ever reacts to them. This host binds a durable queue
/// to the events exchange, reads each CloudEvents envelope, and hands the event to the same
/// <see cref="IEventPublisher"/> (and therefore the same handlers and the same per-module inbox) that the
/// in-process bus uses, so a module's handlers do not know which transport delivered the event.
/// </summary>
/// <remarks>
/// A message is acknowledged only after its handlers finish. A failing handler is retried a few times in
/// process; after that the message goes to <c>&lt;queue&gt;.dead</c> through the broker's dead-letter
/// exchange instead of being lost or retried forever. A message of a type this process does not know is
/// another service's event: it is acknowledged and ignored. The envelope id is the outbox row id, which
/// the inbox uses to run each handler at most once for a redelivered message.
/// </remarks>
public sealed class RabbitMqConsumerHost : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);
    private static readonly ActivitySource ActivitySource = new("CrowdFunding.Consumer");

    private readonly RabbitMqOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EventTypeRegistry _registry;
    private readonly ILogger<RabbitMqConsumerHost> _logger;

    public RabbitMqConsumerHost(
        IOptions<RabbitMqOptions> options,
        IServiceScopeFactory scopeFactory,
        EventTypeRegistry registry,
        ILogger<RabbitMqConsumerHost> logger)
    {
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _registry = registry;
        _logger = logger;
    }

    /// <summary>The queue this host consumes from.</summary>
    public string QueueName => _options.Queue ?? DefaultQueueName();

    /// <summary>The dead-letter queue for messages whose handlers kept failing.</summary>
    public string DeadLetterQueueName => $"{QueueName}.dead";

    private static string DefaultQueueName()
        => $"crowdfunding.{System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name?.ToLowerInvariant() ?? "host"}";

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeUntilLostAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "RabbitMQ consumer for queue {Queue} lost its connection; reconnecting in {Seconds} s.", QueueName, ReconnectDelay.TotalSeconds);

                try
                {
                    await Task.Delay(ReconnectDelay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private async Task ConsumeUntilLostAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            UserName = _options.Username,
            Password = _options.Password,
        };

        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await DeclareTopologyAsync(channel, stoppingToken);
        await channel.BasicQosAsync(0, _options.PrefetchCount, false, stoppingToken);

        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        channel.ChannelShutdownAsync += (_, args) =>
        {
            if (args.Initiator != ShutdownInitiator.Application)
            {
                lost.TrySetResult();
            }

            return Task.CompletedTask;
        };

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) => HandleDeliveryAsync(channel, delivery, stoppingToken);
        await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer, stoppingToken);

        _logger.LogInformation("RabbitMQ consumer listening on queue {Queue} (exchange {Exchange}).", QueueName, _options.Exchange);

        using var stopRegistration = stoppingToken.Register(() => lost.TrySetCanceled(stoppingToken));
        await lost.Task;

        throw new InvalidOperationException("The RabbitMQ channel was closed by the broker.");
    }

    private async Task DeclareTopologyAsync(IChannel channel, CancellationToken cancellationToken)
    {
        var deadExchange = $"{_options.Exchange}.dead";

        await channel.ExchangeDeclareAsync(_options.Exchange, ExchangeType.Topic, durable: true, cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(deadExchange, ExchangeType.Fanout, durable: true, cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(DeadLetterQueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(DeadLetterQueueName, deadExchange, routingKey: string.Empty, cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = deadExchange },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(QueueName, _options.Exchange, routingKey: "#", cancellationToken: cancellationToken);
    }

    private async Task HandleDeliveryAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken stoppingToken)
    {
        try
        {
            var handled = await TryHandleAsync(delivery.Body.ToArray(), stoppingToken);

            if (handled)
            {
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken);
            }
            else
            {
                // requeue: false sends it to the dead-letter exchange, where it waits for a person.
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down: leave the message unacknowledged so the broker redelivers it.
        }
    }

    /// <summary>Returns true when the message is finished with (handled, or deliberately ignored), false when it should be dead-lettered.</summary>
    private async Task<bool> TryHandleAsync(byte[] body, CancellationToken stoppingToken)
    {
        string? typeName = null;
        Guid? messageId = null;
        object? applicationEvent;

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            typeName = root.GetProperty("type").GetString();
            messageId = Guid.TryParse(root.GetProperty("id").GetString(), out var parsed) ? parsed : null;

            if (typeName is null || !_registry.TryResolveLatest(typeName, out var eventType) || eventType is null)
            {
                _logger.LogDebug("Ignoring RabbitMQ event {Type}: this process has no registered event of that name.", typeName);
                return true;
            }

            applicationEvent = root.GetProperty("data").Deserialize(eventType, SerializerOptions);

            if (applicationEvent is null)
            {
                _logger.LogError("RabbitMQ event {Type} {MessageId} had a null body; dead-lettering.", typeName, messageId);
                return false;
            }
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            _logger.LogError(exception, "RabbitMQ message could not be read as an event (type {Type}); dead-lettering.", typeName);
            return false;
        }

        using var activity = ActivitySource.StartActivity($"Consume {typeName}", ActivityKind.Consumer);
        var delay = _options.RetryDelay;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();

                if (messageId is not null)
                {
                    scope.ServiceProvider.GetRequiredService<InboxMessageContext>().MessageId = messageId;
                }

                await scope.ServiceProvider.GetRequiredService<IEventPublisher>().PublishAsync(applicationEvent, stoppingToken);
                return true;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Handling RabbitMQ event {Type} {MessageId} failed, attempt {Attempt}/{Max}.",
                    typeName, messageId, attempt, _options.HandlerAttempts);

                if (attempt >= _options.HandlerAttempts)
                {
                    return false;
                }

                await Task.Delay(delay, stoppingToken);
                delay += delay;
            }
        }
    }
}

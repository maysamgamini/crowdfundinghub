using System.Diagnostics;
using System.Text;
using System.Text.Json;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Messaging;

/// <summary>
/// Publishes application events to a RabbitMQ topic exchange as CNCF CloudEvents v1.0 envelopes,
/// for when a module has been extracted into its own deployable process and other
/// services/microservices need to subscribe to its events over the network. Selected via
/// <c>Messaging:Provider = "RabbitMQ"</c> — application event handlers and outbox processors are
/// unaware of the switch; they still just call <see cref="IMessageBus.PublishAsync"/>.
/// </summary>
public sealed class RabbitMqMessageBus : IMessageBus, IAsyncDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly RabbitMqOptions _options;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;

    public RabbitMqMessageBus(IOptions<RabbitMqOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc/>
    public async Task PublishAsync(object applicationEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applicationEvent);

        var eventType = applicationEvent.GetType();
        var envelope = new CloudEventEnvelope(
            Id: Guid.NewGuid().ToString(),
            Source: "urn:crowdfunding:monolith",
            Type: eventType.Name,
            Time: DateTimeOffset.UtcNow,
            DataContentType: "application/json",
            TraceParent: Activity.Current?.Id,
            Data: applicationEvent);

        var body = JsonSerializer.SerializeToUtf8Bytes(envelope, SerializerOptions);

        var connection = await GetOrCreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(_options.Exchange, ExchangeType.Topic, durable: true, cancellationToken: cancellationToken);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            MessageId = envelope.Id,
            Type = envelope.Type,
            Headers = new Dictionary<string, object?>
            {
                ["ce-id"] = envelope.Id,
                ["ce-source"] = envelope.Source,
                ["ce-type"] = envelope.Type,
                ["ce-specversion"] = CloudEventEnvelope.SpecVersion,
                ["ce-time"] = envelope.Time.ToString("O"),
                ["traceparent"] = envelope.TraceParent ?? string.Empty,
            },
        };

        await channel.BasicPublishAsync(
            exchange: _options.Exchange,
            routingKey: envelope.Type,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }

    private async Task<IConnection> GetOrCreateConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            var factory = new ConnectionFactory
            {
                HostName = _options.Host,
                Port = _options.Port,
                UserName = _options.Username,
                Password = _options.Password,
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _connectionLock.Dispose();
    }
}

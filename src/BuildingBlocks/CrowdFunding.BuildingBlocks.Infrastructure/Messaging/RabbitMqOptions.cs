namespace CrowdFunding.BuildingBlocks.Infrastructure.Messaging;

/// <summary>
/// Connection settings for <see cref="RabbitMqMessageBus"/>, bound from <c>Messaging:RabbitMq</c>.
/// </summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "Messaging:RabbitMq";

    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string Username { get; init; } = "guest";
    public string Password { get; init; } = "guest";

    /// <summary>The topic exchange every published event is routed through.</summary>
    public string Exchange { get; init; } = "crowdfunding.events";

    /// <summary>The durable queue this process consumes from. Defaults to a name built from the host assembly.</summary>
    public string? Queue { get; init; }

    /// <summary>How many unacknowledged messages the broker may hand this consumer at once.</summary>
    public ushort PrefetchCount { get; init; } = 10;

    /// <summary>How many times a handler is tried for one message before it goes to the dead-letter queue.</summary>
    public int HandlerAttempts { get; init; } = 3;

    /// <summary>Pause before the first retry; it doubles for each further attempt.</summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);
}

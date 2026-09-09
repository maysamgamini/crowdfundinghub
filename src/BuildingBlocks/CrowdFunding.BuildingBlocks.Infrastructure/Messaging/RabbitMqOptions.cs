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
}

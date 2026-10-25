namespace CrowdFunding.BuildingBlocks.Infrastructure.Retention;

/// <summary>How long data that has a useful life is kept. Bound from the <c>Retention</c> configuration section.</summary>
public sealed class RetentionOptions
{
    public const string SectionName = "Retention";

    /// <summary>
    /// How long a client may retry a pledge request with the same <c>Idempotency-Key</c> and get the first
    /// pledge back. After this the key is cleared and the same key would create a new pledge.
    /// </summary>
    public int IdempotencyKeyDays { get; init; } = 7;

    /// <summary>
    /// How long a record of "this handler already ran for this message" is kept. It only has to outlive the
    /// longest time a message can still be redelivered, so it is longer than the key window.
    /// </summary>
    public int InboxDays { get; init; } = 14;

    /// <summary>
    /// How long the record of an applied payment webhook event id is kept. A payment provider retries a webhook for
    /// up to 72 hours, so a week is longer than any redelivery that can still arrive.
    /// </summary>
    public int PaymentWebhookDays { get; init; } = 7;

    /// <summary>How often the cleanup runs.</summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromHours(1);
}

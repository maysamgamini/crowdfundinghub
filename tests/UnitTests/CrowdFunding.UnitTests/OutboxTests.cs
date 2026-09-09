using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.BuildingBlocks.Domain.Common;
using CrowdFunding.BuildingBlocks.Infrastructure.Persistence;

namespace CrowdFunding.UnitTests;

public sealed class AdvisoryLockKeyTests
{
    [Fact]
    public void FromGuid_ShouldBeDeterministic()
    {
        var id = Guid.NewGuid();

        Assert.Equal(AdvisoryLockKey.FromGuid(id), AdvisoryLockKey.FromGuid(id));
    }

    [Fact]
    public void FromGuid_ShouldUseAllSixteenBytes_NotJustTheHashCodeTruncation()
    {
        // Two GUIDs engineered to share the same 32-bit Guid.GetHashCode() would have collided
        // under the old `(long)campaignId.GetHashCode()` derivation. Rather than search for such
        // a pair, assert the actual property that fixes the bug: every byte of the GUID
        // participates, so changing any byte changes the key (a hash truncated to 32 bits could
        // never guarantee this for the high-order bytes it discards).
        var original = Guid.NewGuid();
        var bytes = original.ToByteArray();
        bytes[15] ^= 0xFF;
        var mutated = new Guid(bytes);

        Assert.NotEqual(AdvisoryLockKey.FromGuid(original), AdvisoryLockKey.FromGuid(mutated));
    }
}

public sealed class EventTypeRegistryTests
{
    [Fact]
    public void Register_ThenTryResolve_ShouldReturnRegisteredType()
    {
        var registry = new EventTypeRegistry();
        registry.Register(typeof(SampleApplicationEvent));

        var resolved = registry.TryResolve(nameof(SampleApplicationEvent), 1, out var type);

        Assert.True(resolved);
        Assert.Equal(typeof(SampleApplicationEvent), type);
    }

    [Fact]
    public void TryResolve_ShouldReturnFalse_WhenEventTypeIsUnregistered()
    {
        var registry = new EventTypeRegistry();

        var resolved = registry.TryResolve("SomeUnknownEventType", 1, out var type);

        Assert.False(resolved);
        Assert.Null(type);
    }

    [Fact]
    public void TryResolve_ShouldReturnFalse_WhenVersionDoesNotMatch()
    {
        var registry = new EventTypeRegistry();
        registry.Register(typeof(SampleApplicationEvent));

        var resolved = registry.TryResolve(nameof(SampleApplicationEvent), 2, out var type);

        Assert.False(resolved);
    }

    [Fact]
    public void Register_ShouldThrow_WhenTypeIsNotAnApplicationEvent()
    {
        var registry = new EventTypeRegistry();

        var action = () => registry.Register(typeof(string));

        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Register_ShouldUseAttributeVersion_WhenPresent()
    {
        var registry = new EventTypeRegistry();
        registry.Register(typeof(VersionedSampleApplicationEvent));

        Assert.True(registry.TryResolve(nameof(VersionedSampleApplicationEvent), 3, out var type));
        Assert.Equal(typeof(VersionedSampleApplicationEvent), type);
        Assert.False(registry.TryResolve(nameof(VersionedSampleApplicationEvent), 1, out _));
    }

    private sealed class SampleApplicationEvent : BaseApplicationEvent
    {
        public Guid Id { get; init; }
    }

    [EventVersion(3)]
    private sealed class VersionedSampleApplicationEvent : BaseApplicationEvent
    {
    }
}

public sealed class OutboxMessageTests
{
    private static EventTypeRegistry CreateRegistryWithSample()
    {
        var registry = new EventTypeRegistry();
        registry.Register(typeof(SampleApplicationEvent));
        return registry;
    }

    [Fact]
    public void Create_ThenTryResolve_ShouldRoundTripThePayload()
    {
        var registry = CreateRegistryWithSample();
        var applicationEvent = new SampleApplicationEvent { Value = "hello" };
        var message = OutboxMessage.Create(applicationEvent, DateTime.UtcNow);

        var resolved = message.TryResolve(registry, out var deserialized, out var failureReason);

        Assert.True(resolved);
        Assert.Null(failureReason);
        var typed = Assert.IsType<SampleApplicationEvent>(deserialized);
        Assert.Equal("hello", typed.Value);
        Assert.Equal(OutboxMessageStatus.Pending, message.Status);
    }

    [Fact]
    public void TryResolve_ShouldFail_WhenEventTypeIsUnregistered()
    {
        var emptyRegistry = new EventTypeRegistry();
        var message = OutboxMessage.Create(new SampleApplicationEvent { Value = "x" }, DateTime.UtcNow);

        var resolved = message.TryResolve(emptyRegistry, out var deserialized, out var failureReason);

        Assert.False(resolved);
        Assert.Null(deserialized);
        Assert.NotNull(failureReason);
    }

    [Fact]
    public void MarkFailed_ShouldReturnToPendingWithBackoff_WhenUnderMaxAttempts()
    {
        var message = OutboxMessage.Create(new SampleApplicationEvent { Value = "x" }, DateTime.UtcNow);
        var now = DateTime.UtcNow;

        message.MarkFailed("boom", maxAttempts: 5, retryDelay: TimeSpan.FromMinutes(1), nowUtc: now);

        Assert.Equal(OutboxMessageStatus.Pending, message.Status);
        Assert.Equal(1, message.Attempts);
        Assert.Equal("boom", message.Error);
        Assert.True(message.ScheduledAtUtc > now);
    }

    [Fact]
    public void MarkFailed_ShouldMoveToDeadLetter_WhenMaxAttemptsReached()
    {
        var message = OutboxMessage.Create(new SampleApplicationEvent { Value = "x" }, DateTime.UtcNow);
        var now = DateTime.UtcNow;

        for (var i = 0; i < 5; i++)
        {
            message.MarkFailed("boom", maxAttempts: 5, retryDelay: TimeSpan.FromMinutes(1), nowUtc: now);
        }

        Assert.Equal(OutboxMessageStatus.DeadLetter, message.Status);
        Assert.Equal(5, message.Attempts);
    }

    [Fact]
    public void MarkProcessed_ShouldClearErrorAndSetProcessedStatus()
    {
        var message = OutboxMessage.Create(new SampleApplicationEvent { Value = "x" }, DateTime.UtcNow);
        message.MarkFailed("boom", maxAttempts: 5, retryDelay: TimeSpan.FromMinutes(1), nowUtc: DateTime.UtcNow);

        var processedAt = DateTime.UtcNow;
        message.MarkProcessed(processedAt);

        Assert.Equal(OutboxMessageStatus.Processed, message.Status);
        Assert.Equal(processedAt, message.ProcessedOnUtc);
        Assert.Null(message.Error);
    }

    [Fact]
    public void MarkDeadLetter_ShouldBeTerminal_RegardlessOfAttempts()
    {
        var message = OutboxMessage.Create(new SampleApplicationEvent { Value = "x" }, DateTime.UtcNow);

        message.MarkDeadLetter("unresolvable event type");

        Assert.Equal(OutboxMessageStatus.DeadLetter, message.Status);
        Assert.Equal(0, message.Attempts);
        Assert.Equal("unresolvable event type", message.Error);
    }

    [Fact]
    public void Create_ShouldDefaultHeadersToEmptyObject_WhenNoActivityIsAmbient()
    {
        System.Diagnostics.Activity.Current = null;

        var message = OutboxMessage.Create(new SampleApplicationEvent { Value = "x" }, DateTime.UtcNow);

        Assert.Equal("{}", message.Headers);
    }

    [Fact]
    public void Create_ShouldCaptureTraceParent_WhenAnActivityIsAmbient()
    {
        using var activitySource = new System.Diagnostics.ActivitySource("Tests.OutboxMessage");
        using var listener = new System.Diagnostics.ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref System.Diagnostics.ActivityCreationOptions<System.Diagnostics.ActivityContext> _) =>
                System.Diagnostics.ActivitySamplingResult.AllData,
        };
        System.Diagnostics.ActivitySource.AddActivityListener(listener);

        using var activity = activitySource.StartActivity("test-request");
        Assert.NotNull(activity);

        var message = OutboxMessage.Create(new SampleApplicationEvent { Value = "x" }, DateTime.UtcNow);

        var headers = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(message.Headers)!;
        Assert.Equal(activity!.Id, headers["traceparent"]);
    }

    private sealed class SampleApplicationEvent : BaseApplicationEvent
    {
        public string Value { get; init; } = string.Empty;
    }
}

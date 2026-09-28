using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.BuildingBlocks.Infrastructure.Persistence;
using CrowdFunding.Modules.Contributions.Contracts.Events.ContributionPaymentConfirmed;

namespace CrowdFunding.UnitTests;

/// <summary>
/// The inbox keys on <see cref="BaseApplicationEvent.EventId"/>, so it must survive the outbox
/// round trip unchanged and name the outbox row itself.
/// </summary>
public sealed class ApplicationEventIdentityTests
{
    [Fact]
    public void EventId_ShouldSurviveTheOutboxRoundTrip_AndBecomeTheOutboxRowId()
    {
        var applicationEvent = new ContributionPaymentConfirmedApplicationEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 25m, "USD");
        var registry = new EventTypeRegistry();
        registry.Register(typeof(ContributionPaymentConfirmedApplicationEvent));

        var message = OutboxMessage.Create(applicationEvent, DateTime.UtcNow);
        var resolved = message.TryResolve(registry, out var redelivered, out _);

        Assert.True(resolved);
        Assert.Equal(applicationEvent.EventId, message.Id);
        Assert.Equal(applicationEvent.EventId, Assert.IsType<ContributionPaymentConfirmedApplicationEvent>(redelivered).EventId);
    }

    [Fact]
    public void EachNewEvent_ShouldGetItsOwnEventId()
    {
        var first = new ContributionPaymentConfirmedApplicationEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1m, "USD");
        var second = new ContributionPaymentConfirmedApplicationEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1m, "USD");

        Assert.NotEqual(first.EventId, second.EventId);
    }
}

using CrowdFunding.BuildingBlocks.Infrastructure.Outbox;
using CrowdFunding.BuildingBlocks.Infrastructure.Audit;

namespace CrowdFunding.UnitTests;

public sealed class OutboxSignalTests
{
    [Fact]
    public async Task WaitAsync_ShouldReturnTrue_WhenNotifiedBeforeTheFallbackInterval()
    {
        var signal = new OutboxSignal<AuditDbContext>();
        signal.Notify();

        Assert.True(await signal.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None));
    }

    [Fact]
    public async Task WaitAsync_ShouldReturnFalse_WhenTheFallbackIntervalElapsesWithoutANotification()
    {
        var signal = new OutboxSignal<AuditDbContext>();

        Assert.False(await signal.WaitAsync(TimeSpan.FromMilliseconds(20), CancellationToken.None));
    }

    [Fact]
    public async Task ManyNotifications_ShouldCoalesceIntoASingleWakeUp()
    {
        var signal = new OutboxSignal<AuditDbContext>();
        for (var i = 0; i < 100; i++)
        {
            signal.Notify();
        }

        Assert.True(await signal.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None));
        Assert.False(await signal.WaitAsync(TimeSpan.FromMilliseconds(20), CancellationToken.None));
    }

    [Fact]
    public async Task WaitAsync_ShouldThrow_WhenTheHostIsStopping()
    {
        var signal = new OutboxSignal<AuditDbContext>();
        using var stopping = new CancellationTokenSource();
        stopping.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => signal.WaitAsync(TimeSpan.FromSeconds(5), stopping.Token));
    }
}

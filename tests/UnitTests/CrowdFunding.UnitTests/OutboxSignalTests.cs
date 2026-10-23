using CrowdFunding.BuildingBlocks.Infrastructure.Outbox;

namespace CrowdFunding.UnitTests;

public sealed class OutboxSignalTests
{
    private sealed class SomeModule;

    [Fact]
    public async Task Wait_ShouldReturnTrueAtOnce_WhenSignalledBeforeWaiting()
    {
        var signal = new ChannelOutboxSignal<SomeModule>();
        signal.Notify();

        var woken = await signal.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None);

        Assert.True(woken);
    }

    [Fact]
    public async Task Wait_ShouldReturnFalse_WhenNothingSignalsBeforeTheTimeout()
    {
        var signal = new ChannelOutboxSignal<SomeModule>();

        var woken = await signal.WaitAsync(TimeSpan.FromMilliseconds(50), CancellationToken.None);

        Assert.False(woken);
    }

    [Fact]
    public async Task Wait_ShouldWakeAPendingWaiter_WhenSignalledLater()
    {
        var signal = new ChannelOutboxSignal<SomeModule>();
        var waiting = signal.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None).AsTask();

        await Task.Delay(30);
        signal.Notify();

        Assert.True(await waiting.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task ManySignals_ShouldCollapseIntoOneWakeUp()
    {
        var signal = new ChannelOutboxSignal<SomeModule>();
        for (var i = 0; i < 100; i++)
        {
            signal.Notify();
        }

        Assert.True(await signal.WaitAsync(TimeSpan.FromSeconds(1), CancellationToken.None));
        Assert.False(await signal.WaitAsync(TimeSpan.FromMilliseconds(50), CancellationToken.None));
    }

    [Fact]
    public async Task Wait_ShouldThrow_WhenTheCallerCancels()
    {
        var signal = new ChannelOutboxSignal<SomeModule>();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await signal.WaitAsync(TimeSpan.FromSeconds(30), cancellation.Token));
    }
}

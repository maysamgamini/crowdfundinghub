using System.Threading.Channels;
using CrowdFunding.BuildingBlocks.Application.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// <see cref="IOutboxSignal{TModule}"/> over a one-slot <see cref="Channel{T}"/>. Any number of
/// commits that happen while the worker is busy collapse into a single pending wake-up, so there is
/// no queue to grow, and a signal sent before the worker starts waiting is not lost: it stays in the
/// slot and the next wait returns at once.
/// </summary>
public sealed class ChannelOutboxSignal<TModule> : IOutboxSignal<TModule>
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
        SingleWriter = false
    });

    /// <inheritdoc/>
    public void Notify() => _channel.Writer.TryWrite(true);

    /// <inheritdoc/>
    public async ValueTask<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (_channel.Reader.TryRead(out _))
        {
            return true;
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            await _channel.Reader.ReadAsync(timeoutSource.Token);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}

public static class OutboxSignalRegistration
{
    /// <summary>Registers the wake-up signal for one module's outbox worker (safe to call more than once).</summary>
    public static IServiceCollection AddOutboxSignal<TModule>(this IServiceCollection services)
    {
        services.TryAddSingleton<IOutboxSignal<TModule>, ChannelOutboxSignal<TModule>>();
        return services;
    }
}

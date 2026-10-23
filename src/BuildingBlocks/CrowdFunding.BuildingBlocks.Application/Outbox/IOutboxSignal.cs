namespace CrowdFunding.BuildingBlocks.Application.Outbox;

/// <summary>
/// A wake-up call from "a transaction that wrote outbox rows just committed" to "the worker that
/// delivers them". Without it the worker only notices new rows on its next timer tick, so an event
/// waits up to a whole poll interval before anything reacts to it. With it the worker wakes the
/// moment the commit happens, and the timer remains only as a safety net.
/// </summary>
/// <remarks>
/// One signal per module (<typeparamref name="TModule"/> is the module's DbContext type), so a
/// commit in Campaigns wakes only the Campaigns worker. The signal lives in process memory: it is
/// an optimisation, never the source of truth. The outbox table is. If the signal is lost (the
/// process restarts, or another instance wrote the row) the next sweep finds the row anyway.
/// </remarks>
public interface IOutboxSignal<TModule>
{
    /// <summary>Tells the worker there is probably something to deliver. Cheap, never blocks, safe to call many times.</summary>
    void Notify();

    /// <summary>Waits until <see cref="Notify"/> is called or <paramref name="timeout"/> passes. Returns true when woken by a signal.</summary>
    ValueTask<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken);
}

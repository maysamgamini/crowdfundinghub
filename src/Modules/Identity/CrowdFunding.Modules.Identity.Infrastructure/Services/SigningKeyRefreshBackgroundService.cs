using CrowdFunding.Modules.Identity.Application.Abstractions.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CrowdFunding.Modules.Identity.Infrastructure.Services;

/// <summary>
/// The time-based fallback half of TICKET-037's cache coherence strategy: re-runs
/// <see cref="ISigningKeyStore.WarmUpAsync"/> on a fixed interval regardless of whether a
/// Redis Pub/Sub invalidation message was ever received. Pub/Sub delivery is best-effort — a
/// replica that is disconnected from Redis at the exact moment another replica rotates keys
/// would otherwise keep signing/verifying with a stale key set indefinitely, until its next
/// restart. This bounds that staleness window to <see cref="RefreshInterval"/> instead.
/// </summary>
public sealed class SigningKeyRefreshBackgroundService : BackgroundService
{
    // TICKET-037 acceptance criterion 3 names this figure explicitly: "falls back gracefully to
    // a time-based TTL (e.g. 5 minutes)".
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);

    private readonly ISigningKeyStore _signingKeyStore;
    private readonly ILogger<SigningKeyRefreshBackgroundService> _logger;

    public SigningKeyRefreshBackgroundService(ISigningKeyStore signingKeyStore, ILogger<SigningKeyRefreshBackgroundService> logger)
    {
        _signingKeyStore = signingKeyStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RefreshInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await _signingKeyStore.WarmUpAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                // Must never crash the host: this is a periodic self-healing pass, not a
                // request-scoped operation with a caller waiting on its outcome. A failure here
                // (e.g. the database is briefly unreachable) just means this replica keeps
                // serving whatever key set it last successfully loaded until the next tick.
                _logger.LogWarning(exception, "Periodic signing key refresh failed; will retry on the next interval.");
            }
        }
    }
}

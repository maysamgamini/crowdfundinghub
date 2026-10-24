using CrowdFunding.BuildingBlocks.Infrastructure.Hosting;
using CrowdFunding.BuildingBlocks.Infrastructure.Outbox;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Services;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrowdFunding.Modules.Contributions.Infrastructure.Payments;

/// <summary>
/// The settlement worker. When a campaign is funded its pledges wait in the queue; this service claims the ones that
/// are due, a batch at a time, and charges each through <see cref="PledgeChargeService"/>. It does nothing in the
/// charge-now funding model, where there is nothing to settle.
/// </summary>
public sealed class SettlementBackgroundService : BackgroundService, IOutboxDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly PaymentOptions _options;
    private readonly SemaphoreSlim _passGate = new(1, 1);

    public SettlementBackgroundService(IServiceProvider serviceProvider, IOptions<PaymentOptions> options)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
    }

    /// <inheritdoc/>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var logger = _serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<SettlementBackgroundService>();

        return GuardedLoop.RunAsync(async ct =>
        {
            var claimed = await ProcessBatchAsync(ct);

            // A full batch means there is probably more due: go round again at once instead of waiting.
            if (claimed < _options.SettlementBatchSize)
            {
                await Task.Delay(_options.SettlementPollInterval, ct);
            }
        }, _options.SettlementPollInterval, logger, nameof(SettlementBackgroundService), stoppingToken);
    }

    /// <summary>Runs one settlement pass. Public so tests can drive it without waiting for the schedule.</summary>
    async Task IOutboxDispatcher.ProcessBatchAsync(CancellationToken cancellationToken)
        => await ProcessBatchAsync(cancellationToken);

    /// <summary>Claims and charges one batch of due pledges; returns how many were claimed.</summary>
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await _passGate.WaitAsync(cancellationToken);
        try
        {
            await using var scope = _serviceProvider.CreateAsyncScope();

            if (scope.ServiceProvider.GetRequiredService<IPaymentSettings>().FundingModel != FundingModel.SaveThenCharge)
            {
                return 0;
            }

            return await scope.ServiceProvider.GetRequiredService<PledgeChargeService>()
                .ChargeDueAsync(_options.SettlementBatchSize, cancellationToken);
        }
        finally
        {
            _passGate.Release();
        }
    }
}

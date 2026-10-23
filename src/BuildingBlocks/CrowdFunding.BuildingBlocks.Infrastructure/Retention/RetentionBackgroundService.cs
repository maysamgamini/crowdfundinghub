using CrowdFunding.BuildingBlocks.Application.Retention;
using CrowdFunding.BuildingBlocks.Infrastructure.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Retention;

/// <summary>Runs every registered <see cref="IRetentionTask"/> on a schedule. One task failing never stops the others.</summary>
public sealed class RetentionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEnumerable<IRetentionTask> _tasks;
    private readonly RetentionOptions _options;
    private readonly ILogger<RetentionBackgroundService> _logger;

    public RetentionBackgroundService(
        IServiceScopeFactory scopeFactory,
        IEnumerable<IRetentionTask> tasks,
        IOptions<RetentionOptions> options,
        ILogger<RetentionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _tasks = tasks;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => GuardedLoop.RunAsync(async ct =>
        {
            await RunOnceAsync(DateTime.UtcNow, ct);
            await Task.Delay(_options.Interval, ct);
        }, _options.Interval, _logger, nameof(RetentionBackgroundService), stoppingToken);

    /// <summary>Runs every task once. Public so tests can drive it without waiting for the schedule.</summary>
    public async Task RunOnceAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        foreach (var task in _tasks)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var removed = await task.RunAsync(scope.ServiceProvider, nowUtc, cancellationToken);

                if (removed > 0)
                {
                    _logger.LogInformation("Retention task {Task} cleaned up {Count} rows.", task.Name, removed);
                }
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Retention task {Task} failed; the others still run.", task.Name);
            }
        }
    }
}

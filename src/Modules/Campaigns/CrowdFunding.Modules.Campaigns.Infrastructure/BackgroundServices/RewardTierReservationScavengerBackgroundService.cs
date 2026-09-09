using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;
using CrowdFunding.Modules.Campaigns.Application.Features.RewardTiers.Commands.ReleaseExpiredRewardTierReservation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.BackgroundServices;

/// <summary>
/// Reclaims reward tier slot reservations abandoned mid-checkout: a backer who reserves a perk
/// and never completes payment (closed the tab, card declined, walked away) would otherwise hold
/// that slot forever, since nothing else in the system ever calls
/// <c>RewardTier.ReleaseReservation()</c> for a reservation that simply times out. Runs every
/// 2 minutes and releases every <c>Reserved</c> reservation whose 15-minute window
/// (<c>RewardTierReservation.ReservationWindow</c>) has passed. See TICKET-043.
/// </summary>
public sealed class RewardTierReservationScavengerBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(2);
    private const int BatchSize = 50;

    private readonly IServiceProvider _serviceProvider;

    public RewardTierReservationScavengerBackgroundService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessExpiredReservationsAsync(stoppingToken);
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    /// <summary>Runs a single scavenge pass immediately — exposed for integration tests to
    /// trigger deterministically instead of waiting on <see cref="PollInterval"/>.</summary>
    public async Task ProcessExpiredReservationsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var reservationRepository = services.GetRequiredService<IRewardTierReservationRepository>();
        var dateTimeProvider = services.GetRequiredService<IDateTimeProvider>();
        var commandDispatcher = services.GetRequiredService<ICommandDispatcher>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger<RewardTierReservationScavengerBackgroundService>();

        var nowUtc = dateTimeProvider.UtcNow;
        var expired = await reservationRepository.GetExpiredBatchAsync(nowUtc, BatchSize, cancellationToken);

        foreach (var reservation in expired)
        {
            try
            {
                await commandDispatcher.SendAsync<ReleaseExpiredRewardTierReservationResult>(
                    new ReleaseExpiredRewardTierReservationCommand(reservation.Id),
                    cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to release expired reward tier reservation {ReservationId}.", reservation.Id);
            }
        }
    }
}

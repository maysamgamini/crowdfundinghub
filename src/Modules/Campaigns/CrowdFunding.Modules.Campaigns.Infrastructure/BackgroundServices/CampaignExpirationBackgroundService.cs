using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CompleteCampaign;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.FailCampaign;
using CrowdFunding.Modules.Campaigns.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.BackgroundServices;

/// <summary>
/// Resolves every Published campaign whose deadline has passed to Successful (goal reached) or
/// Failed (goal not reached), driving the crowdfunding lifecycle to completion without a manual
/// trigger. Registered by the Campaigns module itself — see
/// <c>CampaignsInfrastructureDependencyInjection</c> — so it moves with the module if Campaigns
/// is ever extracted into its own service.
///
/// A false read here (e.g. the campaign was already resolved by a concurrent instance, or
/// received a contribution between this scan and the command handler's advisory lock) is not a
/// bug: <c>CompleteCampaignCommandHandler</c>/<c>FailCampaignCommandHandler</c> re-load the
/// campaign under an advisory lock and re-validate before mutating, so a stale guess here just
/// throws <see cref="InvalidOperationException"/>, logged and skipped, rather than corrupting state.
/// </summary>
public sealed class CampaignExpirationBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    private readonly IServiceProvider _serviceProvider;

    public CampaignExpirationBackgroundService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessExpiredCampaignsAsync(stoppingToken);
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    /// <summary>Runs a single expiration-resolution pass immediately — exposed for integration
    /// tests to trigger deterministically instead of waiting on <see cref="PollInterval"/>.</summary>
    public async Task ProcessExpiredCampaignsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var campaignRepository = services.GetRequiredService<ICampaignRepository>();
        var dateTimeProvider = services.GetRequiredService<IDateTimeProvider>();
        var commandDispatcher = services.GetRequiredService<ICommandDispatcher>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger<CampaignExpirationBackgroundService>();

        var nowUtc = dateTimeProvider.UtcNow;
        var expiredCampaignIds = await campaignRepository.GetExpiredPublishedCampaignIdsAsync(nowUtc, cancellationToken);

        foreach (var campaignId in expiredCampaignIds)
        {
            try
            {
                var campaign = await campaignRepository.GetByIdAsync(campaignId, cancellationToken);

                if (campaign is null || campaign.Status != CampaignStatus.Published)
                {
                    // Already resolved by a concurrent pass since the scan above.
                    continue;
                }

                if (campaign.RaisedAmount.Amount >= campaign.GoalAmount.Amount)
                {
                    await commandDispatcher.SendAsync<CompleteCampaignResult>(new CompleteCampaignCommand(campaignId), cancellationToken);
                }
                else
                {
                    await commandDispatcher.SendAsync<FailCampaignResult>(new FailCampaignCommand(campaignId), cancellationToken);
                }
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to resolve expired campaign {CampaignId}.", campaignId);
            }
        }
    }
}

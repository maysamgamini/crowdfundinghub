using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCancelled;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCreated;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignFailed;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignPublished;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignSucceeded;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Services;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Transactions;

namespace CrowdFunding.Modules.Pledges.Application.Features.ActiveCampaigns.Events;

/// <summary>
/// Replicates Campaigns' application events into Pledges' own local
/// <see cref="IActiveCampaignCacheRepository"/> (Event-Carried State Transfer), so
/// <c>MakePledgeCommandHandler</c>/<c>ConfirmPledgePaymentCommandHandler</c> validate
/// pledges against a local, sub-millisecond read model instead of a synchronous, in-process call
/// into Campaigns — see TICKET-023. A campaign is replicated inactive at creation time (Draft
/// cannot accept pledges), flipped active on publish, and flipped back inactive on cancellation.
/// The events may arrive in any order (a redelivery, or a batch handled out of order), so no event undoes what a
/// later one set: "created" only starts the row inactive, and a "published" that arrives first leaves a placeholder
/// for "created" to complete.
/// </summary>
public sealed class ReplicatedCampaignEventHandlers :
    IRecordsInboxInTransaction,
    IEventHandler<CampaignCreatedApplicationEvent>,
    IEventHandler<CampaignPublishedApplicationEvent>,
    IEventHandler<CampaignCancelledApplicationEvent>,
    IEventHandler<CampaignSucceededApplicationEvent>,
    IEventHandler<CampaignFailedApplicationEvent>
{
    private readonly IActiveCampaignCacheRepository _repository;
    private readonly IPledgeDateTimeProvider _dateTimeProvider;
    private readonly IPledgeTransactionExecutor _transactionExecutor;

    public ReplicatedCampaignEventHandlers(
        IActiveCampaignCacheRepository repository,
        IPledgeDateTimeProvider dateTimeProvider,
        IPledgeTransactionExecutor transactionExecutor)
    {
        _repository = repository;
        _dateTimeProvider = dateTimeProvider;
        _transactionExecutor = transactionExecutor;
    }

    public Task Handle(CampaignCreatedApplicationEvent notification, CancellationToken cancellationToken)
        => _transactionExecutor.ExecuteAsync(
            ct => _repository.UpsertAsync(
                notification.CampaignId,
                notification.OwnerId,
                notification.Title,
                notification.Currency,
                isActive: false,
                notification.DeadlineUtc,
                _dateTimeProvider.UtcNow,
                ct),
            cancellationToken);

    public Task Handle(CampaignPublishedApplicationEvent notification, CancellationToken cancellationToken)
        => _transactionExecutor.ExecuteAsync(
            ct => _repository.SetActiveStatusAsync(notification.CampaignId, isActive: true, _dateTimeProvider.UtcNow, ct),
            cancellationToken);

    public Task Handle(CampaignCancelledApplicationEvent notification, CancellationToken cancellationToken)
        => _transactionExecutor.ExecuteAsync(
            ct => _repository.SetActiveStatusAsync(notification.CampaignId, isActive: false, _dateTimeProvider.UtcNow, ct),
            cancellationToken);

    public Task Handle(CampaignSucceededApplicationEvent notification, CancellationToken cancellationToken)
        => _transactionExecutor.ExecuteAsync(
            ct => _repository.SetActiveStatusAsync(notification.CampaignId, isActive: false, _dateTimeProvider.UtcNow, ct),
            cancellationToken);

    public Task Handle(CampaignFailedApplicationEvent notification, CancellationToken cancellationToken)
        => _transactionExecutor.ExecuteAsync(
            ct => _repository.SetActiveStatusAsync(notification.CampaignId, isActive: false, _dateTimeProvider.UtcNow, ct),
            cancellationToken);
}

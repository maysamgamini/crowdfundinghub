using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Moderation.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Moderation.Application.Abstractions.Services;
using CrowdFunding.Modules.Moderation.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Moderation.Domain.Aggregates;

namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.CreateCampaignReview;

/// <summary>
/// Handles Create Campaign Review command requests.
/// </summary>
public sealed class CreateCampaignReviewCommandHandler : ICommandHandler<CreateCampaignReviewCommand, CreateCampaignReviewResult>
{
    private readonly ICampaignReviewRepository _campaignReviewRepository;
    private readonly IModerationDateTimeProvider _dateTimeProvider;
    private readonly IModerationTransactionExecutor _transactionExecutor;

    public CreateCampaignReviewCommandHandler(
        ICampaignReviewRepository campaignReviewRepository,
        IModerationDateTimeProvider dateTimeProvider,
        IModerationTransactionExecutor transactionExecutor)
    {
        _campaignReviewRepository = campaignReviewRepository;
        _dateTimeProvider = dateTimeProvider;
        _transactionExecutor = transactionExecutor;
    }

    public async Task<CreateCampaignReviewResult> Handle(
        CreateCampaignReviewCommand command,
        CancellationToken cancellationToken)
    {
        var existingReview = await _campaignReviewRepository.GetByCampaignIdAsync(command.CampaignId, cancellationToken);

        if (existingReview is not null)
        {
            return new CreateCampaignReviewResult(existingReview.Id, existingReview.Status.ToString());
        }

        var campaignReview = CampaignReview.Create(command.CampaignId, _dateTimeProvider.UtcNow);

        await _transactionExecutor.ExecuteAsync(async ct =>
        {
            await _campaignReviewRepository.AddAsync(campaignReview, ct);
            return 0;
        }, cancellationToken);

        return new CreateCampaignReviewResult(campaignReview.Id, campaignReview.Status.ToString());
    }
}

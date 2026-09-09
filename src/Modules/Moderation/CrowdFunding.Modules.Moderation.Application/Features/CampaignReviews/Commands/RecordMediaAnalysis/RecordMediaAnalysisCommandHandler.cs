using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Moderation.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Moderation.Application.Abstractions.Services;
using CrowdFunding.Modules.Moderation.Application.Abstractions.Transactions;

namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.RecordMediaAnalysis;

/// <summary>
/// Handles the result of an asynchronous, serverless media-safety analysis. Deliberately has no
/// <c>ICurrentUser</c> authorization check — this command is only ever dispatched from the
/// anonymous, HMAC-signature-verified webhook controller (a Cloud Function can't present a user
/// JWT); the signature check is that endpoint's actual authentication. See TICKET-032.
/// </summary>
public sealed class RecordMediaAnalysisCommandHandler : ICommandHandler<RecordMediaAnalysisCommand, RecordMediaAnalysisResult>
{
    private readonly ICampaignReviewRepository _campaignReviewRepository;
    private readonly IModerationDateTimeProvider _dateTimeProvider;
    private readonly IModerationTransactionExecutor _transactionExecutor;

    public RecordMediaAnalysisCommandHandler(
        ICampaignReviewRepository campaignReviewRepository,
        IModerationDateTimeProvider dateTimeProvider,
        IModerationTransactionExecutor transactionExecutor)
    {
        _campaignReviewRepository = campaignReviewRepository;
        _dateTimeProvider = dateTimeProvider;
        _transactionExecutor = transactionExecutor;
    }

    public async Task<RecordMediaAnalysisResult> Handle(RecordMediaAnalysisCommand command, CancellationToken cancellationToken)
    {
        var campaignReview = await _campaignReviewRepository.GetByCampaignIdAsync(command.CampaignId, cancellationToken);
        if (campaignReview is null)
        {
            throw new KeyNotFoundException($"Campaign review for campaign '{command.CampaignId}' was not found.");
        }

        await _transactionExecutor.ExecuteAsync(async ct =>
        {
            campaignReview.RecordAutomatedMediaAnalysis(
                command.PassedSafetyCheck, command.ToxicityScore, command.AdultContentScore, _dateTimeProvider.UtcNow);
            await _campaignReviewRepository.UpdateAsync(campaignReview, ct);
        }, cancellationToken);

        return new RecordMediaAnalysisResult(command.CampaignId, campaignReview.Status.ToString());
    }
}

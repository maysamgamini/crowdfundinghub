using CrowdFunding.Modules.Moderation.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Moderation.Application.Abstractions.Services;
using CrowdFunding.Modules.Moderation.Domain.Aggregates;

namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.CreateCampaignReview;

public sealed class CreateCampaignReviewCommandHandler
{
    private readonly ICampaignReviewRepository _campaignReviewRepository;
    private readonly IModerationDateTimeProvider _dateTimeProvider;

    public CreateCampaignReviewCommandHandler(
        ICampaignReviewRepository campaignReviewRepository,
        IModerationDateTimeProvider dateTimeProvider)
    {
        _campaignReviewRepository = campaignReviewRepository;
        _dateTimeProvider = dateTimeProvider;
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

        await _campaignReviewRepository.AddAsync(campaignReview, cancellationToken);

        return new CreateCampaignReviewResult(campaignReview.Id, campaignReview.Status.ToString());
    }
}

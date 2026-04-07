using CreateCampaignReviewApplicationCommand = CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.CreateCampaignReview.CreateCampaignReviewCommand;
using CreateCampaignReviewContractCommand = CrowdFunding.Modules.Moderation.Contracts.Commands.CreateCampaignReview.CreateCampaignReviewCommand;
using CreateCampaignReviewContractResult = CrowdFunding.Modules.Moderation.Contracts.Commands.CreateCampaignReview.CreateCampaignReviewResult;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.CreateCampaignReview;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Queries.GetCampaignReviewStatusByCampaignId;
using CrowdFunding.Modules.Moderation.Contracts;
using CrowdFunding.Modules.Moderation.Contracts.Queries.GetCampaignReviewStatusByCampaignId;

namespace CrowdFunding.Modules.Moderation.Infrastructure.Services;

public sealed class ModerationModule : IModerationModule
{
    private readonly CreateCampaignReviewCommandHandler _createCampaignReviewCommandHandler;
    private readonly GetCampaignReviewStatusByCampaignIdQueryHandler _getCampaignReviewStatusByCampaignIdQueryHandler;

    public ModerationModule(
        CreateCampaignReviewCommandHandler createCampaignReviewCommandHandler,
        GetCampaignReviewStatusByCampaignIdQueryHandler getCampaignReviewStatusByCampaignIdQueryHandler)
    {
        _createCampaignReviewCommandHandler = createCampaignReviewCommandHandler;
        _getCampaignReviewStatusByCampaignIdQueryHandler = getCampaignReviewStatusByCampaignIdQueryHandler;
    }

    public async Task<CreateCampaignReviewContractResult> CreateCampaignReviewAsync(
        CreateCampaignReviewContractCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _createCampaignReviewCommandHandler.Handle(
            new CreateCampaignReviewApplicationCommand(command.CampaignId),
            cancellationToken);

        return new CreateCampaignReviewContractResult(result.CampaignReviewId, result.Status);
    }

    public Task<GetCampaignReviewStatusByCampaignIdResult> GetCampaignReviewStatusByCampaignIdAsync(
        GetCampaignReviewStatusByCampaignIdQuery query,
        CancellationToken cancellationToken)
    {
        return _getCampaignReviewStatusByCampaignIdQueryHandler.Handle(query, cancellationToken);
    }
}

using CrowdFunding.Modules.Campaigns.Contracts;
using CrowdFunding.Modules.Campaigns.Contracts.Commands.AddContributionToCampaign;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Services;
using CrowdFunding.Modules.Contributions.Domain.Enums;

namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.ConfirmContributionPayment;

public sealed class ConfirmContributionPaymentCommandHandler
{
    private readonly ICampaignsModule _campaignsModule;
    private readonly IContributionDateTimeProvider _dateTimeProvider;
    private readonly IContributionRepository _contributionRepository;

    public ConfirmContributionPaymentCommandHandler(
        ICampaignsModule campaignsModule,
        IContributionDateTimeProvider dateTimeProvider,
        IContributionRepository contributionRepository)
    {
        _campaignsModule = campaignsModule;
        _dateTimeProvider = dateTimeProvider;
        _contributionRepository = contributionRepository;
    }

    public async Task<ConfirmContributionPaymentResult> Handle(
        ConfirmContributionPaymentCommand command,
        CancellationToken cancellationToken)
    {
        var contribution = await _contributionRepository.GetByIdAsync(command.ContributionId, cancellationToken);

        if (contribution is null || contribution.CampaignId != command.CampaignId)
        {
            throw new KeyNotFoundException($"Contribution '{command.ContributionId}' was not found for campaign '{command.CampaignId}'.");
        }

        contribution.ConfirmPayment(command.PaymentReference, _dateTimeProvider.UtcNow);

        await _campaignsModule.AddContributionToCampaignAsync(
            new AddContributionToCampaignCommand(
                contribution.CampaignId,
                contribution.Money.Amount,
                contribution.Money.Currency),
            cancellationToken);

        await _contributionRepository.UpdateAsync(contribution, cancellationToken);

        return new ConfirmContributionPaymentResult(
            contribution.Id,
            contribution.Status.ToString(),
            contribution.PaymentReference!);
    }
}

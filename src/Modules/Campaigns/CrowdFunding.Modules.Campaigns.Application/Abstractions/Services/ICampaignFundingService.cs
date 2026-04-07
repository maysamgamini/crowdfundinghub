namespace CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;

public interface ICampaignFundingService
{
    Task AddContributionAsync(
        Guid campaignId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken);
}

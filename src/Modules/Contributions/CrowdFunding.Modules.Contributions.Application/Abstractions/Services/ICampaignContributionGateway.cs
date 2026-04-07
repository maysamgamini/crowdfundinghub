namespace CrowdFunding.Modules.Contributions.Application.Abstractions.Services;

public interface ICampaignContributionGateway
{
    Task ApplyContributionAsync(
        Guid campaignId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken);
}

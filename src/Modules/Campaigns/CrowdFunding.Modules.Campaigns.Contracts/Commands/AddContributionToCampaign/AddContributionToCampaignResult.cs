namespace CrowdFunding.Modules.Campaigns.Contracts.Commands.AddContributionToCampaign;

/// <summary>
/// The outcome of crediting a confirmed contribution to a campaign.
/// </summary>
/// <param name="CampaignId">The campaign.</param>
/// <param name="RaisedAmount">The campaign's raised amount afterwards (the sum of its ledger).</param>
/// <param name="Currency">The campaign's currency.</param>
/// <param name="Credited">
/// <see langword="true"/> when this call appended the pledge; <see langword="false"/> when it was
/// already credited (redelivery) or the campaign no longer accepts funds (cancelled or completed).
/// </param>
public sealed record AddContributionToCampaignResult(
    Guid CampaignId,
    decimal RaisedAmount,
    string Currency,
    bool Credited);

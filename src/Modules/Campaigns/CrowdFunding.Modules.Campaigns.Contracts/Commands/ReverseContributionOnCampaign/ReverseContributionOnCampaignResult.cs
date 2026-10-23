namespace CrowdFunding.Modules.Campaigns.Contracts.Commands.ReverseContributionOnCampaign;

/// <param name="Reversed">True when the raised balance went down; false when nothing changed (already reversed, or the pledge was never counted).</param>
public sealed record ReverseContributionOnCampaignResult(Guid CampaignId, decimal RaisedAmount, string Currency, bool Reversed);

namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.ConfirmPledgeSetup;

/// <summary>The backer's card was saved with the payment provider (the provider's "setup succeeded" news).</summary>
/// <param name="PaymentMethodReference">The provider's reference for the saved card. Never a card number.</param>
public sealed record ConfirmPledgeSetupCommand(Guid CampaignId, Guid ContributionId, string PaymentMethodReference);

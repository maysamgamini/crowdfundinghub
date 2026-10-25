namespace CrowdFunding.API.Contracts.Pledges;

/// <summary>The provider's reference for the card the backer saved. Never a card number.</summary>
public sealed record ConfirmPledgeSetupRequest(string PaymentMethodReference);

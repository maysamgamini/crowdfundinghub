namespace CrowdFunding.API.Contracts.Contributions;

/// <summary>The provider's reference for the card the backer saved. Never a card number.</summary>
public sealed record ConfirmPledgeSetupRequest(string PaymentMethodReference);

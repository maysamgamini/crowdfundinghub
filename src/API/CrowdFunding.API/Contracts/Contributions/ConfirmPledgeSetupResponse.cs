namespace CrowdFunding.API.Contracts.Contributions;

public sealed record ConfirmPledgeSetupResponse(Guid ContributionId, string Status);

namespace CrowdFunding.API.Contracts.Pledges;

public sealed record ConfirmPledgeSetupResponse(Guid PledgeId, string Status);

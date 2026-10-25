namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.ConfirmPledgeSetup;

public sealed record ConfirmPledgeSetupResult(Guid PledgeId, string Status);

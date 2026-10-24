namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.ConfirmPledgeSetup;

public sealed record ConfirmPledgeSetupResult(Guid ContributionId, string Status);

namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.MakePledge;

/// <summary>
/// Represents the outcome returned by Make Pledge.
/// </summary>
public sealed record MakePledgeResult(
    Guid PledgeId,
    string Status,
    bool IsReplay = false,
    string? ClientSecret = null);

namespace CrowdFunding.API.Contracts.Pledges;

/// <summary>
/// Represents the HTTP response payload returned upon creating a pledge.
/// </summary>
/// <param name="PledgeId">The unique identifier of the created pledge.</param>
/// <param name="Status">The initial status of the pledge (Pending).</param>
public sealed record MakePledgeResponse(
    Guid PledgeId,
    string Status,
    string? ClientSecret = null);

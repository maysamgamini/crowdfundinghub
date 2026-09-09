namespace CrowdFunding.API.Contracts.Identity;

/// <summary>
/// Represents the HTTP request payload for exchanging a refresh token for a new access token.
/// </summary>
/// <param name="RefreshToken">The opaque refresh token previously issued by login or a prior refresh call.</param>
public sealed record RefreshAccessTokenRequest(string RefreshToken);

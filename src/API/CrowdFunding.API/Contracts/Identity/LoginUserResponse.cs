namespace CrowdFunding.API.Contracts.Identity;

/// <summary>
/// Represents the HTTP response payload returned upon successful authentication.
/// </summary>
/// <param name="AccessToken">The asymmetric ES256 JSON Web Token (JWT) used for Bearer authentication.</param>
/// <param name="ExpiresAtUtc">The UTC timestamp when the access token expires.</param>
public sealed record LoginUserResponse(
    string AccessToken,
    DateTime ExpiresAtUtc);

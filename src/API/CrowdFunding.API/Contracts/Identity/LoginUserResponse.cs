namespace CrowdFunding.API.Contracts.Identity;

/// <summary>
/// Represents the HTTP response payload returned upon successful authentication.
/// </summary>
/// <param name="AccessToken">The asymmetric ES256 JSON Web Token (JWT) used for Bearer authentication.</param>
/// <param name="ExpiresAtUtc">The UTC timestamp when the access token expires.</param>
/// <param name="RefreshToken">The opaque refresh token used to obtain a new access token via <c>POST /api/identity/refresh</c>.</param>
public sealed record LoginUserResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string RefreshToken);

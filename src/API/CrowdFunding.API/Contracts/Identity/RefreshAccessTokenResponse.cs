namespace CrowdFunding.API.Contracts.Identity;

/// <summary>
/// Represents the HTTP response payload returned after a successful refresh-token rotation.
/// </summary>
/// <param name="AccessToken">The newly issued asymmetric ES256 JSON Web Token (JWT).</param>
/// <param name="ExpiresAtUtc">The UTC timestamp when the new access token expires.</param>
/// <param name="RefreshToken">The newly issued opaque refresh token; the one presented in the request is now revoked.</param>
public sealed record RefreshAccessTokenResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string RefreshToken);

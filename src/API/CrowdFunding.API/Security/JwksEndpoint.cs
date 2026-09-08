using CrowdFunding.Modules.Identity.Application.Abstractions.Services;
using Microsoft.IdentityModel.Tokens;

namespace CrowdFunding.API.Security;

/// <summary>
/// Publishes the public half of the active ES256 signing key(s) at
/// <c>/.well-known/jwks.json</c> (RFC 7517), so a downstream verifier (API gateway, another
/// microservice) can validate tokens without ever holding the private key
/// (improvement.md §2.8/§3.8 #2). Built manually rather than via
/// <see cref="JsonWebKeyConverter"/> so there is no code path that could ever attempt to export
/// a private component — <see cref="ISigningKeyStore.GetPublicSigningKeys"/> only ever returns
/// public-only ECDsa instances to begin with.
/// </summary>
public static class JwksEndpoint
{
    /// <summary>
    /// Serves the public JWKS JSON payload containing active ES256 verification keys.
    /// </summary>
    /// <param name="signingKeyStore">The asymmetric signing key store service.</param>
    /// <returns>A JSON result conforming to RFC 7517 JSON Web Key Set.</returns>
    public static IResult Get(ISigningKeyStore signingKeyStore)
    {
        var keys = signingKeyStore.GetPublicSigningKeys().Select(key =>
        {
            var parameters = key.Key.ExportParameters(includePrivateParameters: false);

            return new
            {
                kty = "EC",
                crv = "P-256",
                use = "sig",
                alg = "ES256",
                kid = key.Kid,
                x = Base64UrlEncoder.Encode(parameters.Q.X),
                y = Base64UrlEncoder.Encode(parameters.Q.Y),
            };
        });

        return Results.Json(new { keys });
    }
}

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CrowdFunding.Moderation.Service.Security;

/// <summary>
/// Resolves JWT Bearer signing keys by fetching the RFC 7517 JWKS document the monolith's
/// Identity module publishes at <c>/.well-known/jwks.json</c> — the same decentralized,
/// offline-verification contract any other downstream verifier (a gateway, another extracted
/// service) would use. This is the crux of the "friction-free extraction" claim: this class is
/// the ONLY place that knows tokens exist, and it never sees a private key or a shared secret.
/// Keys are cached in memory for <see cref="CacheDuration"/> since they only change when
/// Identity rotates its signing key, which is rare compared to request volume.
/// </summary>
public sealed class JwksSigningKeyResolver
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    private readonly HttpClient _httpClient;
    private readonly JwksOptions _options;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private IReadOnlyList<SecurityKey> _cachedKeys = [];
    private DateTimeOffset _cachedAtUtc = DateTimeOffset.MinValue;

    public JwksSigningKeyResolver(HttpClient httpClient, IOptions<JwksOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    /// <summary>
    /// Matches <see cref="Microsoft.IdentityModel.Tokens.TokenValidationParameters.IssuerSigningKeyResolver"/>'s
    /// delegate signature so it can be assigned directly during JWT Bearer configuration.
    /// </summary>
    public IEnumerable<SecurityKey> ResolveSigningKeys(
        string token, SecurityToken securityToken, string? kid, TokenValidationParameters validationParameters)
    {
        var keys = GetKeysAsync().GetAwaiter().GetResult();
        return kid is null ? keys : keys.Where(key => key.KeyId == kid);
    }

    private async Task<IReadOnlyList<SecurityKey>> GetKeysAsync()
    {
        if (DateTimeOffset.UtcNow - _cachedAtUtc < CacheDuration && _cachedKeys.Count > 0)
        {
            return _cachedKeys;
        }

        await _refreshLock.WaitAsync();
        try
        {
            if (DateTimeOffset.UtcNow - _cachedAtUtc < CacheDuration && _cachedKeys.Count > 0)
            {
                return _cachedKeys;
            }

            using var response = await _httpClient.GetAsync(_options.JwksUri);
            response.EnsureSuccessStatusCode();

            using var payload = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
            var keys = new List<SecurityKey>();

            foreach (var jwk in payload.RootElement.GetProperty("keys").EnumerateArray())
            {
                var x = Base64UrlEncoder.DecodeBytes(jwk.GetProperty("x").GetString());
                var y = Base64UrlEncoder.DecodeBytes(jwk.GetProperty("y").GetString());
                var kid = jwk.GetProperty("kid").GetString();

                var ecdsa = ECDsa.Create(new ECParameters
                {
                    Curve = ECCurve.NamedCurves.nistP256,
                    Q = new ECPoint { X = x, Y = y }
                });

                keys.Add(new ECDsaSecurityKey(ecdsa) { KeyId = kid });
            }

            _cachedKeys = keys;
            _cachedAtUtc = DateTimeOffset.UtcNow;
            return _cachedKeys;
        }
        finally
        {
            _refreshLock.Release();
        }
    }
}

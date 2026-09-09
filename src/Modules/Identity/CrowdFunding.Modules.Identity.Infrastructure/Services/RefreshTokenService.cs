using System.Security.Cryptography;
using CrowdFunding.Modules.Identity.Application.Abstractions.Services;
using Microsoft.Extensions.Configuration;

namespace CrowdFunding.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Generates 256-bit CSPRNG refresh tokens and hashes them with SHA-256 for storage. Unlike
/// <see cref="Pbkdf2PasswordHasher"/>, no per-token salt or deliberately slow KDF is needed: a
/// raw refresh token is never user-chosen or reused across accounts, so its 256 bits of entropy
/// alone already make brute force from a leaked hash infeasible — the concern PBKDF2 exists to
/// address for passwords (low entropy, human-chosen, reused) simply doesn't apply here.
/// </summary>
public sealed class RefreshTokenService : IRefreshTokenService
{
    private const int TokenSizeBytes = 32;

    public RefreshTokenService(IConfiguration configuration)
    {
        var lifetimeDays = int.TryParse(configuration[$"{JwtOptions.SectionName}:RefreshTokenExpirationDays"], out var days)
            ? days
            : 30;

        Lifetime = TimeSpan.FromDays(lifetimeDays);
    }

    public TimeSpan Lifetime { get; }

    public string GenerateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenSizeBytes));

    public string Hash(string rawToken) => Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken)));
}

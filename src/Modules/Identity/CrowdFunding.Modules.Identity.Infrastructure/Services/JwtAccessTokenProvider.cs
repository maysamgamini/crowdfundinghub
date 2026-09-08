using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CrowdFunding.Modules.Identity.Application.Abstractions.Services;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using CrowdFunding.Modules.Identity.Domain.Aggregates;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CrowdFunding.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Generates signed JWT access tokens for authenticated users.
/// </summary>
public sealed class JwtAccessTokenProvider : IAccessTokenProvider
{
    private readonly IIdentityDateTimeProvider _dateTimeProvider;
    private readonly ISigningKeyStore _signingKeyStore;
    private readonly JwtOptions _options;

    public JwtAccessTokenProvider(
        IConfiguration configuration,
        IIdentityDateTimeProvider dateTimeProvider,
        ISigningKeyStore signingKeyStore)
    {
        _dateTimeProvider = dateTimeProvider;
        _signingKeyStore = signingKeyStore;
        _options = new JwtOptions
        {
            Issuer = configuration[$"{JwtOptions.SectionName}:Issuer"] ?? string.Empty,
            Audience = configuration[$"{JwtOptions.SectionName}:Audience"] ?? string.Empty,
            ExpirationMinutes = int.TryParse(configuration[$"{JwtOptions.SectionName}:ExpirationMinutes"], out var minutes)
                ? minutes
                : 60
        };

        if (string.IsNullOrWhiteSpace(_options.Issuer) || string.IsNullOrWhiteSpace(_options.Audience))
        {
            throw new InvalidOperationException("JWT settings are not configured correctly.");
        }
    }

    public AccessToken Create(User user, IReadOnlyCollection<string> permissions)
    {
        var issuedAtUtc = _dateTimeProvider.UtcNow;
        var expiresAtUtc = issuedAtUtc.AddMinutes(_options.ExpirationMinutes);
        var signingKey = _signingKeyStore.GetActiveSigningKey();
        var securityKey = new ECDsaSecurityKey(signingKey.Key) { KeyId = signingKey.Kid };
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.EcdsaSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(CustomClaimTypes.DisplayName, user.DisplayName)
        };

        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role.Role)));
        claims.AddRange(permissions.Select(permission => new Claim(CustomClaimTypes.Permission, permission)));

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: issuedAtUtc,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new AccessToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAtUtc);
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using CrowdFunding.Modules.Identity.Application.Abstractions.Services;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using CrowdFunding.Modules.Identity.Domain.Aggregates;
using CrowdFunding.Modules.Identity.Infrastructure.Persistence;
using CrowdFunding.Modules.Identity.Infrastructure.Services;
using Microsoft.Extensions.Configuration;

namespace CrowdFunding.UnitTests;

public sealed class SigningKeyRecordTests
{
    [Fact]
    public void Deactivate_ShouldTurnOffIsActive_WithoutClearingTheKeyMaterial()
    {
        var record = new SigningKeyRecord("kid-1", "base64-key", DateTime.UtcNow);

        record.Deactivate();

        Assert.False(record.IsActive);
        Assert.Equal("kid-1", record.Kid);
        Assert.Equal("base64-key", record.PrivateKeyPkcs8Base64);
    }
}

public sealed class Pbkdf2PasswordHasherTests
{
    [Fact]
    public void HashPassword_ShouldProduceIterationsSaltAndHash_SeparatedByDots()
    {
        var hasher = new Pbkdf2PasswordHasher();

        var hash = hasher.HashPassword("SuperSecret123!");

        var parts = hash.Split('.');
        Assert.Equal(3, parts.Length);
        Assert.True(int.Parse(parts[0]) > 0);
    }

    [Fact]
    public void HashPassword_ShouldProduceDifferentHashes_ForSamePasswordDueToRandomSalt()
    {
        var hasher = new Pbkdf2PasswordHasher();

        var first = hasher.HashPassword("SuperSecret123!");
        var second = hasher.HashPassword("SuperSecret123!");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void HashPassword_ShouldThrow_WhenPasswordIsBlank()
    {
        var hasher = new Pbkdf2PasswordHasher();

        var action = () => hasher.HashPassword("   ");

        var exception = Assert.Throws<ArgumentException>(action);
        Assert.Equal("Password is required. (Parameter 'password')", exception.Message);
    }

    [Fact]
    public void VerifyPassword_ShouldReturnTrue_ForCorrectPassword()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var hash = hasher.HashPassword("SuperSecret123!");

        Assert.True(hasher.VerifyPassword(hash, "SuperSecret123!"));
    }

    [Fact]
    public void VerifyPassword_ShouldReturnFalse_ForIncorrectPassword()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var hash = hasher.HashPassword("SuperSecret123!");

        Assert.False(hasher.VerifyPassword(hash, "wrong-password"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-valid-hash-format")]
    [InlineData("100000.onlyonepart")]
    public void VerifyPassword_ShouldReturnFalse_ForMalformedHash(string malformedHash)
    {
        var hasher = new Pbkdf2PasswordHasher();

        Assert.False(hasher.VerifyPassword(malformedHash, "whatever"));
    }

    [Fact]
    public void VerifyPassword_ShouldReturnFalse_WhenPasswordIsBlank()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var hash = hasher.HashPassword("SuperSecret123!");

        Assert.False(hasher.VerifyPassword(hash, "   "));
    }

    [Fact]
    public void DummyHash_ShouldBeDeterministic_AcrossInstances()
    {
        var first = new Pbkdf2PasswordHasher();
        var second = new Pbkdf2PasswordHasher();

        Assert.Equal(first.DummyHash, second.DummyHash);
    }

    [Fact]
    public void VerifyPassword_ShouldReturnFalse_AgainstDummyHash()
    {
        var hasher = new Pbkdf2PasswordHasher();

        Assert.False(hasher.VerifyPassword(hasher.DummyHash, "anything"));
    }
}

public sealed class JwtAccessTokenProviderTests
{
    private static IConfiguration BuildConfiguration(string? issuer = "CrowdFunding.API", string? audience = "CrowdFunding.Client", string expirationMinutes = "60")
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = issuer,
                ["Jwt:Audience"] = audience,
                ["Jwt:ExpirationMinutes"] = expirationMinutes,
            })
            .Build();
    }

    private static User CreateUserWithRoleAndReturnPermissions(out IReadOnlyCollection<string> permissions)
    {
        var user = User.Register("creator@example.com", "Creator", "hash", DateTime.UtcNow);
        user.AssignRole(RoleConstants.Creator);
        permissions = [PermissionConstants.CampaignsCreate];
        return user;
    }

    [Fact]
    public void Create_ShouldProduceEs256SignedTokenWithExpectedClaims()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signingKeyStore = new FakeSigningKeyStore(new SigningKeyMaterial("test-kid", ecdsa));
        var dateTimeProvider = new FakeIdentityDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));
        var provider = new JwtAccessTokenProvider(BuildConfiguration(), dateTimeProvider, signingKeyStore);
        var user = CreateUserWithRoleAndReturnPermissions(out var permissions);

        var token = provider.Create(user, permissions);

        Assert.Equal(dateTimeProvider.UtcNow.AddMinutes(60), token.ExpiresAtUtc);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.Value);
        Assert.Equal("ES256", jwt.Header.Alg);
        Assert.Equal("test-kid", jwt.Header.Kid);
        Assert.Equal("CrowdFunding.API", jwt.Issuer);
        Assert.Contains("CrowdFunding.Client", jwt.Audiences);
        Assert.Equal(user.Id.ToString(), jwt.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal(user.Email, jwt.Claims.First(c => c.Type == ClaimTypes.Email).Value);
        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.Role && c.Value == RoleConstants.Creator);
        Assert.Contains(jwt.Claims, c => c.Type == CustomClaimTypes.Permission && c.Value == PermissionConstants.CampaignsCreate);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenIssuerIsMissing()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signingKeyStore = new FakeSigningKeyStore(new SigningKeyMaterial("test-kid", ecdsa));
        var dateTimeProvider = new FakeIdentityDateTimeProvider(DateTime.UtcNow);

        var action = () => new JwtAccessTokenProvider(BuildConfiguration(issuer: null), dateTimeProvider, signingKeyStore);

        var exception = Assert.Throws<InvalidOperationException>(action);
        Assert.Equal("JWT settings are not configured correctly.", exception.Message);
    }

    [Fact]
    public void Constructor_ShouldDefaultExpirationTo15Minutes_WhenNotConfigured()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signingKeyStore = new FakeSigningKeyStore(new SigningKeyMaterial("test-kid", ecdsa));
        var dateTimeProvider = new FakeIdentityDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));
        var provider = new JwtAccessTokenProvider(BuildConfiguration(expirationMinutes: "not-a-number"), dateTimeProvider, signingKeyStore);
        var user = CreateUserWithRoleAndReturnPermissions(out var permissions);

        var token = provider.Create(user, permissions);

        Assert.Equal(dateTimeProvider.UtcNow.AddMinutes(15), token.ExpiresAtUtc);
    }
}

internal sealed class FakeSigningKeyStore : ISigningKeyStore
{
    private readonly SigningKeyMaterial _key;

    public FakeSigningKeyStore(SigningKeyMaterial key)
    {
        _key = key;
    }

    public Task WarmUpAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public SigningKeyMaterial GetActiveSigningKey() => _key;

    public IReadOnlyList<SigningKeyMaterial> GetPublicSigningKeys() => [_key];

    public Task RotateAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

using System.Security.Cryptography;
using CrowdFunding.Modules.Identity.Application.Abstractions.Services;
using CrowdFunding.Modules.Identity.Infrastructure.Persistence;
using CrowdFunding.Modules.Identity.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Identity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// ES256 private keys are encrypted at rest, and rows written before encryption existed are
/// re-protected by the next warm-up (the data half of the migration).
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class SigningKeyEncryptionE2ETests
{
    private readonly CrowdFundingApiFactory _factory;

    public SigningKeyEncryptionE2ETests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task StoredPrivateKeys_ShouldBeEncrypted()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var stored = await dbContext.SigningKeys.Select(key => key.ProtectedPrivateKey).ToListAsync();

        Assert.NotEmpty(stored);
        Assert.All(stored, value => Assert.StartsWith(SigningKeyProtector.ProtectedPrefix, value));
    }

    [Fact]
    public async Task LegacyPlaintextRow_ShouldBeReProtectedOnWarmUp_AndStillVerifyTokens()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var legacy = new SigningKeyRecord(Guid.NewGuid().ToString("N"), Convert.ToBase64String(ecdsa.ExportPkcs8PrivateKey()), DateTime.UtcNow.AddYears(-1));
        legacy.Deactivate();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            dbContext.SigningKeys.Add(legacy);
            await dbContext.SaveChangesAsync();
        }

        var store = _factory.Services.GetRequiredService<ISigningKeyStore>();
        await store.WarmUpAsync(CancellationToken.None);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var reloaded = await dbContext.SigningKeys.AsNoTracking().SingleAsync(key => key.Kid == legacy.Kid);
            Assert.True(SigningKeyProtector.IsProtected(reloaded.ProtectedPrivateKey));
        }

        // The retired key's public half is still published, so tokens it signed keep verifying.
        var published = Assert.Single(store.GetPublicSigningKeys(), key => key.Kid == legacy.Kid);
        Assert.Equal(ecdsa.ExportParameters(false).Q.X, published.Key.ExportParameters(false).Q.X);
    }
}

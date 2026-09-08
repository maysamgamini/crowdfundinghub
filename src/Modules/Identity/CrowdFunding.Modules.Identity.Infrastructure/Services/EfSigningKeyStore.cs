using System.Security.Cryptography;
using CrowdFunding.Modules.Identity.Application.Abstractions.Services;
using CrowdFunding.Modules.Identity.Infrastructure.Persistence;
using CrowdFunding.Modules.Identity.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Persists ES256 (ECDSA P-256) signing keys in <see cref="IdentityDbContext"/> and caches them
/// in memory after <see cref="WarmUpAsync"/>. Registered as a singleton — the DbContext it needs
/// is scoped, so every DB access goes through a short-lived scope created from
/// <see cref="IServiceScopeFactory"/> rather than holding a captive scoped dependency.
/// </summary>
public sealed class EfSigningKeyStore : ISigningKeyStore
{
    private readonly IServiceScopeFactory _scopeFactory;
    private SigningKeyMaterial? _activeSigningKey;
    private IReadOnlyList<SigningKeyMaterial>? _publicSigningKeys;

    public EfSigningKeyStore(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc/>
    public async Task WarmUpAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var activeRecord = await dbContext.SigningKeys
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeRecord is null)
        {
            activeRecord = GenerateNewKeyRecord();
            // Add (not AddAsync) — EF's AddAsync exists only for value generators that need
            // async DB access, which SigningKeyRecord's client-generated Guid key doesn't use.
            dbContext.SigningKeys.Add(activeRecord);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var privateKey = ECDsa.Create();
        privateKey.ImportPkcs8PrivateKey(Convert.FromBase64String(activeRecord.PrivateKeyPkcs8Base64), out _);
        _activeSigningKey = new SigningKeyMaterial(activeRecord.Kid, privateKey);

        // The JWKS list only ever needs the public half — derive it from the same key's exported
        // public parameters rather than round-tripping through storage a second time.
        var publicOnly = ECDsa.Create();
        publicOnly.ImportParameters(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = privateKey.ExportParameters(false).Q,
        });
        _publicSigningKeys = [new SigningKeyMaterial(activeRecord.Kid, publicOnly)];
    }

    /// <inheritdoc/>
    public SigningKeyMaterial GetActiveSigningKey()
        => _activeSigningKey ?? throw new InvalidOperationException(
            $"{nameof(EfSigningKeyStore)} was not warmed up. Call {nameof(WarmUpAsync)} during application startup.");

    /// <inheritdoc/>
    public IReadOnlyList<SigningKeyMaterial> GetPublicSigningKeys()
        => _publicSigningKeys ?? throw new InvalidOperationException(
            $"{nameof(EfSigningKeyStore)} was not warmed up. Call {nameof(WarmUpAsync)} during application startup.");

    private static SigningKeyRecord GenerateNewKeyRecord()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var privateKeyBase64 = Convert.ToBase64String(ecdsa.ExportPkcs8PrivateKey());
        var kid = Guid.NewGuid().ToString("N");

        return new SigningKeyRecord(kid, privateKeyBase64, DateTime.UtcNow);
    }
}

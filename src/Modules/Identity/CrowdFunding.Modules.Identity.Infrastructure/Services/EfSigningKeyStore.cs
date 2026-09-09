using System.Security.Cryptography;
using CrowdFunding.BuildingBlocks.Infrastructure.Caching;
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
///
/// TICKET-037: this in-memory cache is exactly the kind of uncoordinated per-replica state that
/// breaks the moment the monolith runs behind a load balancer with more than one instance — a key
/// rotation on one replica would otherwise leave every other replica signing/verifying with a
/// retired key until its own next restart. <see cref="RotateAsync"/> closes that gap by
/// publishing to the <see cref="SigningKeysInvalidationChannel"/> Redis channel after committing
/// the new key, which every replica's <see cref="DistributedCacheInvalidationSubscriber"/>
/// (wired up in <see cref="DependencyInjection.IdentityInfrastructureDependencyInjection"/>) reacts
/// to by calling <see cref="WarmUpAsync"/> again. <see cref="Services.SigningKeyRefreshBackgroundService"/>
/// additionally re-runs <see cref="WarmUpAsync"/> on a fixed timer, so a replica that misses the
/// Pub/Sub message (e.g. it was disconnected from Redis at the moment of rotation) still
/// self-heals within that timer's period instead of staying stale indefinitely.
/// </summary>
public sealed class EfSigningKeyStore : ISigningKeyStore
{
    public const string SigningKeysInvalidationChannel = "cache:invalidate:signing_keys";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDistributedCacheInvalidationPublisher _invalidationPublisher;
    private SigningKeyMaterial? _activeSigningKey;
    private IReadOnlyList<SigningKeyMaterial>? _publicSigningKeys;

    public EfSigningKeyStore(IServiceScopeFactory scopeFactory, IDistributedCacheInvalidationPublisher invalidationPublisher)
    {
        _scopeFactory = scopeFactory;
        _invalidationPublisher = invalidationPublisher;
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
        var newActiveSigningKey = new SigningKeyMaterial(activeRecord.Kid, privateKey);

        // The public JWKS list includes every known key, not just the active one: a token signed
        // moments before a rotation is still unexpired and must keep verifying against its own
        // (now-retired) key until it naturally expires.
        var allRecords = await dbContext.SigningKeys
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var newPublicSigningKeys = allRecords
            .Select(record =>
            {
                using var recordPrivateKey = ECDsa.Create();
                recordPrivateKey.ImportPkcs8PrivateKey(Convert.FromBase64String(record.PrivateKeyPkcs8Base64), out _);

                var publicOnly = ECDsa.Create();
                publicOnly.ImportParameters(new ECParameters
                {
                    Curve = ECCurve.NamedCurves.nistP256,
                    Q = recordPrivateKey.ExportParameters(false).Q,
                });

                return new SigningKeyMaterial(record.Kid, publicOnly);
            })
            .ToArray();

        // A plain reference reassignment, not a mutation of the existing list/key in place: any
        // request concurrently mid-flight through GetActiveSigningKey()/GetPublicSigningKeys()
        // keeps whatever consistent (old or new, never partial) snapshot it already read.
        // Deliberately not disposing the previous ECDsa instances here — this runs at most once
        // per rotation (an infrequent admin action, not a per-request path), and disposing them
        // while a request already in flight might still be using them to verify a signature would
        // trade a negligible, one-time native-handle leak for a real ObjectDisposedException race.
        _activeSigningKey = newActiveSigningKey;
        _publicSigningKeys = newPublicSigningKeys;
    }

    /// <inheritdoc/>
    public SigningKeyMaterial GetActiveSigningKey()
        => _activeSigningKey ?? throw new InvalidOperationException(
            $"{nameof(EfSigningKeyStore)} was not warmed up. Call {nameof(WarmUpAsync)} during application startup.");

    /// <inheritdoc/>
    public IReadOnlyList<SigningKeyMaterial> GetPublicSigningKeys()
        => _publicSigningKeys ?? throw new InvalidOperationException(
            $"{nameof(EfSigningKeyStore)} was not warmed up. Call {nameof(WarmUpAsync)} during application startup.");

    /// <inheritdoc/>
    public async Task RotateAsync(CancellationToken cancellationToken)
    {
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

            var currentlyActive = await dbContext.SigningKeys
                .Where(x => x.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var record in currentlyActive)
            {
                record.Deactivate();
            }

            dbContext.SigningKeys.Add(GenerateNewKeyRecord());
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // Reload this replica's own in-memory cache immediately — it must not wait for its own
        // Pub/Sub message to round-trip through Redis before it starts signing with the new key.
        await WarmUpAsync(cancellationToken);

        await _invalidationPublisher.PublishAsync(SigningKeysInvalidationChannel, cancellationToken);
    }

    private static SigningKeyRecord GenerateNewKeyRecord()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var privateKeyBase64 = Convert.ToBase64String(ecdsa.ExportPkcs8PrivateKey());
        var kid = Guid.NewGuid().ToString("N");

        return new SigningKeyRecord(kid, privateKeyBase64, DateTime.UtcNow);
    }
}

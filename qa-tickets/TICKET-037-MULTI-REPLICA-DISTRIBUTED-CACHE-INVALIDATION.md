# QA Ticket: TICKET-037

**Title:** Multi-Replica Distributed Cache Invalidation via Redis Pub/Sub & Post-Commit Hooks  
**Severity:** 🟠 P1 (High - Multi-Replica Monolith Scaling & Cache Consistency)  
**QA Focus Area:** High Availability, Distributed Caching & Split-Brain Prevention  
**Found By:** `qa-platform-shortcomings`  
**Status:** Open  
**Project Mode:** Greenfield (Benchmark Educational Standard)  

---

## 1. Description & Architectural Context

In the current codebase, key material and query caching exhibit two critical scaling flaws:

1. **Singleton In-Memory Keystore Caching:** [`EfSigningKeyStore.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Identity/CrowdFunding.Modules.Identity.Infrastructure/Services/EfSigningKeyStore.cs#L19-L21) is registered as a Singleton in DI. Keys are loaded once during startup (`WarmUpAsync`) into private instance fields (`_activeSigningKey`, `_publicSigningKeys`).
2. **Pre-Commit Cache Eviction Hazard:** In [`CampaignRepository.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Infrastructure/Persistence/Repositories/CampaignRepository.cs#L42-L57):
   ```csharp
   _dbContext.Campaigns.Update(campaign);
   // Flaw: Cache is removed BEFORE transaction commits!
   await _cache.RemoveAsync(CampaignCacheKeys.Details(campaign.Id), cancellationToken);
   ```

### The Multi-Replica Split-Brain Failure
When the Modular Monolith scales horizontally (e.g., 3 Kubernetes pods or cloud containers behind a load balancer):
- If Pod A generates a new active signing key or rotates keys, **Pods B and C have no notification mechanism**. They continue signing tokens with the retired key or failing token verification, resulting in intermittent `401 Unauthorized` errors.
- If Pod A invalidates a campaign Redis cache key *before* `SaveChangesAsync` commits to PostgreSQL, a concurrent read on Pod B fetches the **uncommitted old database row** and repopulates the Redis cache with stale data for 30 seconds!

---

## 2. Blast Radius & High-Availability Impact

- **Horizontal Scaling Impairment:** The monolith cannot safely run with replica counts $> 1$ without risking cryptographic split-brain and stale cache corruption.
- **Intermittent Auth Failures:** Users experience random 401 errors depending on which container replica terminates their SSL connection.

---

## 3. Educational Rationale: Teaching Principals & Architects

### The Pedagogical Objective
Teach the prerequisites of **Horizontal Scalability for Monolithic Architectures**. Architects must realize that before a monolith can be broken into microservices, the monolith itself must be cloud-native and stateless—capable of running across multiple load-balanced instances without relying on uncoordinated local in-memory state.

### Monolith First, Microservices Ready
The outcome of this project is a **Modular Monolith, NOT microservices**. However, modern production monoliths run across multiple containers. By implementing:
1. **Distributed Cache Synchronization via Redis Pub/Sub** invalidation channels, and
2. **Post-Commit Invalidation Hooks** in the transaction executor,
the monolith scales horizontally across any number of compute instances seamlessly. When a module is eventually extracted into an autonomous microservice, **its caching layer already adheres to distributed cache coherence standards**.

### What Breaks Tomorrow If Ignored Today?
If an architect ignores cache coordination in a multi-instance monolith, introducing a second container replica in production immediately causes subtle, non-deterministic bugs: stale data being served after updates and random authentication failures upon key rotation.

---

## 4. Affected Files & Modules

- [`src/Modules/Identity/CrowdFunding.Modules.Identity.Infrastructure/Services/EfSigningKeyStore.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Identity/CrowdFunding.Modules.Identity.Infrastructure/Services/EfSigningKeyStore.cs)
- [`src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Infrastructure/Persistence/Repositories/CampaignRepository.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Infrastructure/Persistence/Repositories/CampaignRepository.cs)
- [`src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Infrastructure/Transactions/CampaignTransactionExecutor.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Infrastructure/Transactions/CampaignTransactionExecutor.cs)

---

## 5. Implementation Specification & Greenfield Solution

```mermaid
sequenceDiagram
    autonumber
    participant Admin as Admin / Background Key Job
    participant PodA as Monolith Pod A
    participant RedisPub as Redis Pub/Sub Channel
    participant PodB as Monolith Pod B
    participant PodC as Monolith Pod C

    Admin->>PodA: Rotate Signing Keys
    PodA->>PodA: Save new key to PostgreSQL
    PodA->>RedisPub: PUBLISH "cache:invalidate:signing_keys"
    Note over PodA: Pod A updates local memory
    
    RedisPub-->>PodB: Invalidation Message
    RedisPub-->>PodC: Invalidation Message
    
    PodB->>PodB: Reload signing keys from DB!
    PodC->>PodC: Reload signing keys from DB!
    Note over PodA,PodC: Zero split-brain! All replicas synchronized!
```

### Step 1: Implement Redis Pub/Sub Invalidation Subscriber
Create `DistributedCacheInvalidator` in `BuildingBlocks.Infrastructure`:
```csharp
namespace CrowdFunding.BuildingBlocks.Infrastructure.Caching;

public sealed class DistributedCacheInvalidator : IHostedService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceProvider _serviceProvider;

    public DistributedCacheInvalidator(IConnectionMultiplexer redis, IServiceProvider serviceProvider)
    {
        _redis = redis;
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var sub = _redis.GetSubscriber();
        await sub.SubscribeAsync(RedisChannel.Literal("cache:invalidate:signing_keys"), async (ch, msg) =>
        {
            using var scope = _serviceProvider.CreateScope();
            var keyStore = scope.ServiceProvider.GetRequiredService<ISigningKeyStore>();
            await keyStore.WarmUpAsync(CancellationToken.None);
        });
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

### Step 2: Post-Commit Cache Eviction Hook
Update `CampaignTransactionExecutor` or repository to defer cache eviction:
```csharp
public async Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> action, CancellationToken ct)
{
    var pendingCacheKeys = new List<string>();
    // Store cache keys to invalidate in AsyncLocal execution context
    
    var result = await _strategy.ExecuteAsync(async () =>
    {
        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
        var res = await action();
        await _dbContext.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return res;
    });

    // Invalidate ONLY AFTER transaction is successfully committed to DB!
    foreach (var key in pendingCacheKeys)
    {
        await _cache.RemoveAsync(key, ct);
    }

    return result;
}
```

---

## 6. Verification & Acceptance Criteria

1. **Multi-Instance Invalidation Simulation:** An integration test spinning up two distinct `ServiceProvider` scopes connected to the same Redis instance verifies that rotating keys on Instance A triggers reload on Instance B within 100ms.
2. **Zero Pre-Commit Eviction:** Verify that during a campaign balance update, cache eviction occurs strictly after `tx.CommitAsync()` succeeds.
3. **Resilience to Redis Disconnection:** If Redis Pub/Sub is temporarily unreachable, key reloading falls back gracefully to a time-based TTL (e.g. 5 minutes) without crashing the application.

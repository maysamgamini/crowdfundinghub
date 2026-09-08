# QA Ticket: TICKET-003

**Title:** `Guid.GetHashCode()` Truncation in PostgreSQL Advisory Lock Causes High Collision Rate and Cross-Campaign Contention  
**Severity:** 🟠 P1 (High - Concurrency Bottleneck & False Contention)  
**QA Focus Area:** Concurrency, Race Conditions & Database QA  
**Found By:** `qa-concurrency-financial` / `qa-performance-persistence`  
**Status:** Fixed  
**Project Mode:** Greenfield (No backward compatibility required)  

---

## 1. Description
In `AddContributionToCampaignCommandHandler.cs`, a PostgreSQL transaction-scoped advisory lock is used to serialize writes for a campaign:

```csharp
// Derive a deterministic 64-bit key from the campaign id so every writer targeting the
// same campaign contends for the same pg_advisory_xact_lock, serializing the
// read-modify-write below across concurrent instances/requests.
var advisoryLockKey = unchecked((long)command.CampaignId.GetHashCode());
```

However:
1. `Guid.GetHashCode()` in .NET produces a 32-bit signed integer (`int32`).
2. Casting `(long)GetHashCode()` sign-extends a 32-bit integer into 64 bits without introducing 64 bits of entropy.
3. In a 32-bit integer space ($2^{32} \approx 4.29 \times 10^9$ possibilities), the birthday paradox dictates that a 50% probability of collision occurs after only ~77,163 items. Even with only 1,000 active campaigns, collision probability is non-negligible ($p \approx 0.011$).
4. When two different campaigns collide on the same 32-bit hash, writes to Campaign X will block writes to Campaign Y, causing unexplained latency spikes, lock timeouts, and thread starvation under load.

## 2. Blast Radius & Defect Reproduction
1. Campaign 1 (`CampaignId = Guid.Parse("...")`) and Campaign 2 (`CampaignId = Guid.Parse("...")`) yield identical 32-bit hash codes.
2. Backers simultaneously pledge to Campaign 1 and Campaign 2.
3. PostgreSQL session for Campaign 2 halts on `pg_advisory_xact_lock` waiting for Campaign 1's transaction to commit, even though they represent completely independent business entities.

## 3. Affected Files
- [`src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/Campaigns/Commands/AddContributionToCampaign/AddContributionToCampaignCommandHandler.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/Campaigns/Commands/AddContributionToCampaign/AddContributionToCampaignCommandHandler.cs#L44)

## 4. Recommended Fix (Greenfield)
Derive a full 64-bit integer directly from the 128-bit GUID bytes:
```csharp
public static class AdvisoryLockKeyHelper
{
    public static long FromGuid(Guid id)
    {
        Span<byte> bytes = stackalloc byte[16];
        id.TryWriteBytes(bytes);
        var high = BitConverter.ToInt64(bytes[..8]);
        var low = BitConverter.ToInt64(bytes[8..]);
        return high ^ low;
    }
}
```
In `AddContributionToCampaignCommandHandler`:
```csharp
var advisoryLockKey = AdvisoryLockKeyHelper.FromGuid(command.CampaignId);
```
This distributes the entropy across all 64 bits of PostgreSQL's `bigint` advisory lock space ($2^{64}$ possibilities, where collision probability reaches 50% only after $\approx 5.1 \times 10^9$ campaigns).

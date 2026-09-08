# QA Ticket: TICKET-004

**Title:** `CancelCampaignCommandHandler` Lacks Advisory Locking and Pre-Loads Entity Outside Transaction Boundary  
**Severity:** 🟠 P1 (High - Race Condition & Invariant Violation)  
**QA Focus Area:** Concurrency, Race Conditions & Domain Invariant QA  
**Found By:** `qa-concurrency-financial` / `qa-functional-domain`  
**Status:** Fixed  
**Project Mode:** Greenfield (No backward compatibility required)  

---

## 1. Description
While `AddContributionToCampaignCommandHandler` was hardened with `pg_advisory_xact_lock` and entity reloading inside the transaction, `CancelCampaignCommandHandler` remains completely unhardened:

```csharp
// CancelCampaignCommandHandler.cs
public async Task<CancelCampaignResult> Handle(CancelCampaignCommand command, CancellationToken cancellationToken)
{
    var campaign = await _campaignRepository.GetByIdAsync(command.CampaignId, cancellationToken);
    if (campaign is null)
    {
        throw new KeyNotFoundException($"Campaign with id '{command.CampaignId}' was not found.");
    }

    EnsureCanManageCampaign(campaign.OwnerId);

    await _transactionExecutor.ExecuteAsync(async ct =>
    {
        campaign.Cancel();
        await _campaignRepository.UpdateAsync(campaign, ct);
        return 0;
    }, cancellationToken);

    return new CancelCampaignResult(campaign.Id, campaign.Status.ToString());
}
```

Issues:
1. `campaign` is loaded on line 30 *before* beginning the transaction.
2. `_transactionExecutor.ExecuteAsync` is called *without* passing the campaign's advisory lock key.
3. If an incoming pledge is being processed via `AddContributionToCampaignCommandHandler`, it acquires the advisory lock, but `CancelCampaignCommandHandler` ignores the advisory lock and executes concurrently.
4. Furthermore, in `Campaign.Cancel()`:
   ```csharp
   public void Cancel()
   {
       if (Status == CampaignStatus.Successful || Status == CampaignStatus.Failed)
       {
           throw new InvalidOperationException("Completed campaigns cannot be cancelled.");
       }

       Status = CampaignStatus.Cancelled;
       AddDomainEvent(new CampaignCancelledDomainEvent(Id, OwnerId));
   }
   ```
   If `Cancel()` is invoked on an already `Cancelled` campaign, it succeeds silently, re-sets `Status = CampaignStatus.Cancelled`, and emits another duplicate `CampaignCancelledDomainEvent`.

## 2. Blast Radius & Defect Reproduction
1. Creator sends `Cancel` request.
2. Simultaneously, a webhook or outbox worker applies a confirmed contribution.
3. Because `Cancel` does not acquire `pg_advisory_xact_lock`, both operations interleave.
4. A pledge may be accepted onto a campaign that was just cancelled, or a cancellation might overwrite raised funds.
5. In addition, repeated calls to `POST /api/campaigns/{id}/cancel` flood the outbox with duplicate `CampaignCancelledDomainEvent`s.

## 3. Affected Files
- [`src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/Campaigns/Commands/CancelCampaign/CancelCampaignCommandHandler.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/Campaigns/Commands/CancelCampaign/CancelCampaignCommandHandler.cs#L28-L46)
- [`src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Domain/Aggregates/Campaign.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Domain/Aggregates/Campaign.cs#L97-L106)

## 4. Recommended Fix (Greenfield)
1. Tighten invariant in `Campaign.Cancel()`:
   ```csharp
   public void Cancel()
   {
       if (Status == CampaignStatus.Cancelled)
       {
           throw new InvalidOperationException("Campaign is already cancelled.");
       }

       if (Status == CampaignStatus.Successful || Status == CampaignStatus.Failed)
       {
           throw new InvalidOperationException("Completed campaigns cannot be cancelled.");
       }

       Status = CampaignStatus.Cancelled;
       AddDomainEvent(new CampaignCancelledDomainEvent(Id, OwnerId));
   }
   ```
2. In `CancelCampaignCommandHandler`, acquire the advisory lock and re-fetch the entity inside the lock:
   ```csharp
   var lockKey = AdvisoryLockKeyHelper.FromGuid(command.CampaignId);
   await _transactionExecutor.ExecuteAsync(lockKey, async ct =>
   {
       var campaign = await _campaignRepository.GetByIdAsync(command.CampaignId, ct)
           ?? throw new KeyNotFoundException($"Campaign with id '{command.CampaignId}' was not found.");

       EnsureCanManageCampaign(campaign.OwnerId);
       campaign.Cancel();
       await _campaignRepository.UpdateAsync(campaign, ct);
       return 0;
   }, cancellationToken);
   ```

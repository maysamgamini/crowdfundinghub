# QA Ticket: TICKET-005

**Title:** Missing Database Indexes on `contributions.campaign_id` and `contributions.contributor_id` Cause Sequential Scans  
**Severity:** 🟡 P2 (Medium - Performance & Scalability)  
**QA Focus Area:** Performance & Database Persistence QA  
**Found By:** `qa-performance-persistence`  
**Status:** Open  
**Project Mode:** Greenfield (No backward compatibility required)  

---

## 1. Description
In `ContributionConfiguration.cs`, the entity mapping is configured as follows:

```csharp
builder.ToTable("contributions");

builder.HasKey(x => x.Id);

builder.Property(x => x.CampaignId)
    .IsRequired();

builder.Property(x => x.ContributorId)
    .IsRequired();
```

Notice that neither `CampaignId` nor `ContributorId` has an index defined on the `contributions` table.

## 2. Blast Radius & Defect Reproduction
1. The primary query endpoint in `ContributionsController` is:
   `GET /api/campaigns/{campaignId}/contributions`
2. This invokes `ListContributionsByCampaignQueryHandler`, executing:
   ```sql
   SELECT * FROM contributions WHERE campaign_id = @p0 ORDER BY created_at_utc DESC
   ```
3. Because there is no index on `campaign_id` (nor a composite index on `(campaign_id, created_at_utc)`), PostgreSQL must perform a full sequential scan (`Seq Scan`) across the entire `contributions` table for every single query request.
4. As the platform scales to hundreds of thousands of pledges, database CPU utilization will spike, latency will degrade exponentially, and read connections will become exhausted.

## 3. Affected Files
- [`src/Modules/Contributions/CrowdFunding.Modules.Contributions.Infrastructure/Persistence/Configurations/ContributionConfiguration.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Infrastructure/Persistence/Configurations/ContributionConfiguration.cs#L23-L27)

## 4. Recommended Fix (Greenfield)
Add appropriate B-tree indexes in `ContributionConfiguration.cs`:
```csharp
builder.HasIndex(x => x.CampaignId);
builder.HasIndex(x => x.ContributorId);
builder.HasIndex(x => new { x.CampaignId, x.CreatedAtUtc });
```
Generate and apply the EF Core migration to add these indexes to the `contributions` schema.

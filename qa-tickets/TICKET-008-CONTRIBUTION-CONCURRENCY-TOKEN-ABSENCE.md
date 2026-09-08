# QA Ticket: TICKET-008

**Title:** `Contribution` Aggregate Missing Optimistic Concurrency Token (`xmin`) Allows Race Conditions on Payment State Transitions  
**Severity:** 🟠 P1 (High - Concurrency & Financial State Machine Invariant)  
**QA Focus Area:** Concurrency & Financial Consistency QA  
**Found By:** `qa-concurrency-financial`  
**Status:** Fixed  
**Project Mode:** Greenfield (No backward compatibility required)  

---

## 1. Description
While `CampaignConfiguration.cs` was hardened with PostgreSQL's `xmin` system column as an optimistic concurrency token, `ContributionConfiguration.cs` was omitted and lacks any concurrency token.

In `ContributionConfiguration.cs`:
```csharp
builder.ToTable("contributions");
builder.HasKey(x => x.Id);
...
// No concurrency token configured!
```

## 2. Blast Radius & Defect Reproduction
1. Backer initiates a payment.
2. Payment gateway issues a webhook for confirmation (`ConfirmContributionPaymentCommand`).
3. Simultaneously, a timeout monitor or customer service agent triggers a payment failure/cancellation (`FailContributionPaymentCommand`).
4. Both handlers execute:
   - Handler A reads `Contribution` (Status = `Pending`).
   - Handler B reads `Contribution` (Status = `Pending`).
   - Handler A calls `ConfirmPayment(...)` -> status becomes `Succeeded`.
   - Handler B calls `FailPayment(...)` -> status becomes `Failed`.
5. Because EF Core does not issue a concurrency check (`WHERE id = @p0 AND xmin = @p1`), the last committing transaction overwrites the state of the first transaction without error!
6. If Handler A committed first, Handler B's write overwrites it, marking a paid contribution as `Failed` in the database, while the domain event `ContributionPaymentConfirmedDomainEvent` was already emitted into the outbox by Handler A!

## 3. Affected Files
- [`src/Modules/Contributions/CrowdFunding.Modules.Contributions.Infrastructure/Persistence/Configurations/ContributionConfiguration.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Infrastructure/Persistence/Configurations/ContributionConfiguration.cs)

## 4. Recommended Fix (Greenfield)
Add the PostgreSQL `xmin` concurrency token to `ContributionConfiguration.cs`:
```csharp
builder.Property<uint>("xmin")
    .HasColumnName("xmin")
    .HasColumnType("xid")
    .ValueGeneratedOnAddOrUpdate()
    .IsConcurrencyToken();
```
Add integration tests verifying that concurrent attempts to confirm and fail the same pending contribution trigger a `ConcurrencyConflictException` and prevent state overwrite.

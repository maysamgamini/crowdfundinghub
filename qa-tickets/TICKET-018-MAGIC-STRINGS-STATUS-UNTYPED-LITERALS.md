# QA Ticket: TICKET-018

**Title:** Untyped Magic Strings for Statuses Across Cross-Module Contracts, Handlers, and Read Services  
**Severity:** 🟡 P2 (Medium - Type Safety & Contract Integrity)  
**QA Focus Area:** Cross-Module Contracts & Clean Architecture  
**Found By:** `qa-code-cleanliness`  
**Status:** Open  
**Project Mode:** Greenfield (No backward compatibility required)  

---

## 1. Description
Status checks and query results across module boundaries frequently rely on untyped, raw magic string literals (`"Published"`, `"Approved"`, `"Missing"`) rather than domain enums, contract enums, or shared typed constants:

1. **Cross-Module Availability Checks with Magic Strings**:
   In [`ConfirmContributionPaymentCommandHandler.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Application/Features/Contributions/Commands/ConfirmContributionPayment/ConfirmContributionPaymentCommandHandler.cs#L55):
   ```csharp
   if (!string.Equals(campaignAvailability.Status, "Published", StringComparison.OrdinalIgnoreCase))
   {
       throw new InvalidOperationException("Contribution payments can only be confirmed while the campaign is published.");
   }
   ```
   In [`PublishCampaignCommandHandler.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/Campaigns/Commands/PublishCampaign/PublishCampaignCommandHandler.cs#L50):
   ```csharp
   if (!string.Equals(review.Status, "Approved", StringComparison.OrdinalIgnoreCase))
   {
       throw new InvalidOperationException("Campaign must be approved by moderation before it can be published.");
   }
   ```

2. **Magic String Values in Query Handlers**:
   In [`GetCampaignContributionAvailabilityQueryHandler.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/Campaigns/Queries/GetCampaignContributionAvailability/GetCampaignContributionAvailabilityQueryHandler.cs#L28):
   ```csharp
   if (campaign is null)
   {
       return new GetCampaignContributionAvailabilityResult(query.CampaignId, false, false, "Missing", null);
   }
   ```
   Here `"Missing"` is an arbitrary string sentinel returned when a campaign does not exist, alongside `campaign.Status.ToString()`.

3. **Untyped Contract DTOs**:
   In contract assemblies:
   - [`GetCampaignContributionAvailabilityResult.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Contracts/Queries/GetCampaignContributionAvailability/GetCampaignContributionAvailabilityResult.cs#L10):
     `public sealed record GetCampaignContributionAvailabilityResult(Guid CampaignId, bool Exists, bool CanAcceptContributions, string Status, string? Currency);`
   - [`GetCampaignReviewStatusByCampaignIdResult.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Moderation/CrowdFunding.Modules.Moderation.Contracts/Queries/GetCampaignReviewStatusByCampaignId/GetCampaignReviewStatusByCampaignIdResult.cs#L6):
     `public sealed record GetCampaignReviewStatusByCampaignIdResult(Guid CampaignId, string Status);`
   Both expose `string Status` instead of strongly-typed contract enums.

4. **Inconsistent Status Filtering in Read Services**:
   - In [`CampaignReadService.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Infrastructure/Services/CampaignReadService.cs#L68-L75): parses strings cleanly using `Enum.TryParse<CampaignStatus>(filter.Status, true, out var status)`.
   - In [`ContributionReadService.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Infrastructure/Services/ContributionReadService.cs#L45): executes LINQ query `x.Status.ToString() == status`, which performs case-sensitive comparison and risks translating into inefficient SQL comparisons or failing when casing differs.

## 2. Blast Radius & Defect Reproduction
1. Refactoring or renaming an enum member (e.g. renaming `CampaignReviewStatus.Approved` to `Accepted`) will not trigger compiler warnings in `PublishCampaignCommandHandler.cs`, causing silent runtime failures.
2. Typos in strings (e.g. `"published"` vs `"Published"` or `"Approved "`) are unchecked by the compiler and Roslyn analyzers.
3. Inconsistent query behavior: `GET /api/contributions?status=confirmed` fails to find records because `status.Trim()` is matched case-sensitively against `Status.ToString()` in PostgreSQL, whereas `GET /api/campaigns?status=published` works due to `Enum.TryParse(..., ignoreCase: true)`.

## 3. Affected Files
- [`src/Modules/Contributions/CrowdFunding.Modules.Contributions.Application/Features/Contributions/Commands/ConfirmContributionPayment/ConfirmContributionPaymentCommandHandler.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Application/Features/Contributions/Commands/ConfirmContributionPayment/ConfirmContributionPaymentCommandHandler.cs#L55)
- [`src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/Campaigns/Commands/PublishCampaign/PublishCampaignCommandHandler.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/Campaigns/Commands/PublishCampaign/PublishCampaignCommandHandler.cs#L50)
- [`src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/Campaigns/Queries/GetCampaignContributionAvailability/GetCampaignContributionAvailabilityQueryHandler.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/Campaigns/Queries/GetCampaignContributionAvailability/GetCampaignContributionAvailabilityQueryHandler.cs#L28)
- [`src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Contracts/Queries/GetCampaignContributionAvailability/GetCampaignContributionAvailabilityResult.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Contracts/Queries/GetCampaignContributionAvailability/GetCampaignContributionAvailabilityResult.cs#L10)
- [`src/Modules/Moderation/CrowdFunding.Modules.Moderation.Contracts/Queries/GetCampaignReviewStatusByCampaignId/GetCampaignReviewStatusByCampaignIdResult.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Moderation/CrowdFunding.Modules.Moderation.Contracts/Queries/GetCampaignReviewStatusByCampaignId/GetCampaignReviewStatusByCampaignIdResult.cs#L6)
- [`src/Modules/Contributions/CrowdFunding.Modules.Contributions.Infrastructure/Services/ContributionReadService.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Infrastructure/Services/ContributionReadService.cs#L45)

## 4. Recommended Fix (Greenfield)
1. **Define Strongly-Typed Contract Enums or Status Constants**:
   In `CrowdFunding.Modules.Campaigns.Contracts`:
   ```csharp
   public enum CampaignStatusContract
   {
       Draft = 1,
       Published = 2,
       Successful = 3,
       Failed = 4,
       Cancelled = 5
   }
   ```
   In `CrowdFunding.Modules.Moderation.Contracts`:
   ```csharp
   public enum CampaignReviewStatusContract
   {
       Pending = 1,
       Approved = 2,
       Rejected = 3
   }
   ```

2. **Update Cross-Module Result Contracts**:
   ```csharp
   public sealed record GetCampaignContributionAvailabilityResult(
       Guid CampaignId,
       bool Exists,
       bool CanAcceptContributions,
       CampaignStatusContract? Status,
       string? Currency);

   public sealed record GetCampaignReviewStatusByCampaignIdResult(
       Guid CampaignId,
       CampaignReviewStatusContract Status);
   ```

3. **Replace Magic String Comparisons**:
   In `ConfirmContributionPaymentCommandHandler.cs`:
   ```csharp
   if (campaignAvailability.Status != CampaignStatusContract.Published)
   {
       throw new InvalidOperationException("Contribution payments can only be confirmed while the campaign is published.");
   }
   ```
   In `PublishCampaignCommandHandler.cs`:
   ```csharp
   if (review.Status != CampaignReviewStatusContract.Approved)
   {
       throw new InvalidOperationException("Campaign must be approved by moderation before it can be published.");
   }
   ```

4. **Standardize Query Filtering in `ContributionReadService`**:
   Use `Enum.TryParse<ContributionStatus>(filter.Status, ignoreCase: true, out var status)`:
   ```csharp
   if (!string.IsNullOrWhiteSpace(filter.Status))
   {
       if (Enum.TryParse<ContributionStatus>(filter.Status, true, out var status))
       {
           query = query.Where(x => x.Status == status);
       }
       else
       {
           query = query.Where(_ => false);
       }
   }
   ```

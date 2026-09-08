# QA Ticket: TICKET-001

**Title:** Unvalidated Currency in `MakeContribution` Causes Outbox Processing Failure and Permanent Dead-Lettering (DLQ)  
**Severity:** 🔴 P0 (Critical - Financial Data Loss & Queue Poisoning)  
**QA Focus Area:** Concurrency, Financial & Resilience QA  
**Found By:** `qa-functional-domain` / `qa-concurrency-financial` / `qa-resilience-outbox`  
**Status:** Fixed  
**Project Mode:** Greenfield (No backward compatibility required)  

---

## 1. Description
When a backer contributes to a campaign via `POST /api/campaigns/{campaignId}/contributions`, the `MakeContributionCommandHandler` checks whether the campaign exists and can accept contributions by calling `ICampaignContributionAvailabilityReader.GetCampaignContributionAvailabilityAsync(...)`.

However, `GetCampaignContributionAvailabilityResult` only returns:
```csharp
public sealed record GetCampaignContributionAvailabilityResult(
    Guid CampaignId,
    bool Exists,
    bool CanAcceptContributions,
    string Status);
```
It does **NOT** return the campaign's expected currency (e.g. `USD`). Consequently, `MakeContributionCommandHandler` and `MakeContributionCommandValidator` never validate whether the currency specified in the contribution request matches the campaign's target currency.

## 2. Blast Radius & Defect Reproduction
1. Campaign `A` is created with goal amount in `USD`.
2. Backer initiates a contribution of `100.00 EUR` to Campaign `A`. `MakeContribution` succeeds, creating a `Contribution` in `Pending` state.
3. Payment is confirmed via `ConfirmContributionPaymentCommandHandler`. Status becomes `Succeeded`, and `ContributionPaymentConfirmedDomainEvent` is published and written to `contributions_outbox_messages`.
4. `OutboxProcessorBackgroundService` picks up the message and publishes `ContributionPaymentConfirmedApplicationEvent(Amount = 100.00, Currency = "EUR")`.
5. `ContributionPaymentConfirmedApplicationEventHandler` forwards `AddContributionToCampaignCommand` to `Campaigns` module.
6. `AddContributionToCampaignCommandHandler` calls:
   ```csharp
   campaign.ApplyConfirmedContribution(new Money(command.Amount, command.Currency));
   ```
7. Inside `Money.Add(Money other)`:
   ```csharp
   if (!Currency.Equals(other.Currency, StringComparison.OrdinalIgnoreCase))
   {
       throw new InvalidOperationException("Money currency mismatch.");
   }
   ```
8. The handler throws `InvalidOperationException("Money currency mismatch.")`.
9. `OutboxProcessorBackgroundService` catches the exception, increments attempts, and retries 5 times before permanently moving the event to `dead_letter_events`.
10. **Outcome:** The user's card was charged and marked `Succeeded` in Contributions, but the Campaign's `RaisedAmount` is never credited, the outbox suffers repeated retries, and manual database intervention is required to recover funds.

## 3. Affected Files
- [`src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Contracts/Queries/GetCampaignContributionAvailability/GetCampaignContributionAvailabilityResult.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Contracts/Queries/GetCampaignContributionAvailability/GetCampaignContributionAvailabilityResult.cs)
- [`src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/Campaigns/Queries/GetCampaignContributionAvailability/GetCampaignContributionAvailabilityQueryHandler.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/Campaigns/Queries/GetCampaignContributionAvailability/GetCampaignContributionAvailabilityQueryHandler.cs)
- [`src/Modules/Contributions/CrowdFunding.Modules.Contributions.Application/Features/Contributions/Commands/MakeContribution/MakeContributionCommandHandler.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Application/Features/Contributions/Commands/MakeContribution/MakeContributionCommandHandler.cs)

## 4. Recommended Fix (Greenfield)
1. Add `string Currency` to `GetCampaignContributionAvailabilityResult`:
   ```csharp
   public sealed record GetCampaignContributionAvailabilityResult(
       Guid CampaignId,
       bool Exists,
       bool CanAcceptContributions,
       string Status,
       string Currency);
   ```
2. In `MakeContributionCommandHandler`, enforce:
   ```csharp
   if (!string.Equals(campaignAvailability.Currency, command.Currency, StringComparison.OrdinalIgnoreCase))
   {
       throw new InvalidOperationException(
           $"Contribution currency '{command.Currency}' does not match campaign currency '{campaignAvailability.Currency}'.");
   }
   ```
3. Add a unit test in `MakeContributionCommandHandlerTests` to verify rejection of mismatched currencies before any payment record is created.

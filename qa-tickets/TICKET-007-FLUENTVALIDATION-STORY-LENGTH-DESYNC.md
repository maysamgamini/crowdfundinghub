# QA Ticket: TICKET-007

**Title:** Validation Asymmetry: `CreateCampaignCommandValidator` Missing Minimum Story Length Enforced by Domain Entity  
**Severity:** 🟡 P2 (Medium - Validation Pipeline & User Experience)  
**QA Focus Area:** Functional & API Contracts QA  
**Found By:** `qa-functional-domain` / `qa-api-contracts`  
**Status:** Resolved (Fixed via MinimumLength(20) rule in CreateCampaignCommandValidator)  
**Project Mode:** Greenfield (No backward compatibility required)  

---

## 1. Description
There is a discrepancy between the input validation rules in `CreateCampaignCommandValidator` and the domain invariant in `Campaign.cs`:

In `Campaign.cs`:
```csharp
private static void ValidateStory(string story)
{
    if (string.IsNullOrWhiteSpace(story))
    {
        throw new ArgumentException("Campaign story is required.", nameof(story));
    }

    if (story.Trim().Length < 20)
    {
        throw new ArgumentException("Campaign story must be at least 20 characters.", nameof(story));
    }
}
```

In `CreateCampaignCommandValidator.cs`:
```csharp
RuleFor(x => x.Story)
    .NotEmpty()
    .MaximumLength(5000);
```

Notice that `CreateCampaignCommandValidator` does not specify `.MinimumLength(20)`.

## 2. Blast Radius & Defect Reproduction
1. User sends `POST /api/Campaigns` with `story = "Too short"`.
2. `_createCampaignValidator.ValidateAsync` executes: it passes!
3. `_commandDispatcher.SendAsync` executes `CreateCampaignCommandHandler`.
4. `Campaign.Create(...)` throws `ArgumentException("Campaign story must be at least 20 characters.")`.
5. `GlobalExceptionHandler` intercepts the exception and returns a generic `400 Bad Request` with `{ title: "Bad Request", detail: "Campaign story must be at least 20 characters." }` instead of an RFC 9457 `ValidationProblemDetails` containing structured field-specific errors (`errors: { "Story": ["Campaign story must be at least 20 characters."] }`).

## 3. Affected Files
- [`src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/Campaigns/Commands/CreateCampaign/CreateCampaignCommandValidator.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/Campaigns/Commands/CreateCampaign/CreateCampaignCommandValidator.cs#L16-L18)

## 4. Recommended Fix (Greenfield)
Update `CreateCampaignCommandValidator.cs`:
```csharp
RuleFor(x => x.Story)
    .NotEmpty()
    .MinimumLength(20)
    .MaximumLength(5000);
```
Ensure consistent minimum and maximum length bounds across all command validators and domain value objects.

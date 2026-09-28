---
name: qa-domain-verification
description: >-
  Use this skill to perform functional, black-box, and gray-box QA testing and code review
  focusing on domain invariants, aggregate lifecycles, business logic rules, and edge-case inputs.
---

# Domain Verification & Business Invariant QA Runbook

## Purpose
This skill guides the QA engineer/agent in conducting deep manual and automated verification of business logic, state machines, aggregate invariants, and cross-module workflows in greenfield .NET domain architectures.

## Verification Checklist

### 1. Aggregate Invariants & Boundary Conditions
- **Entity Creation**: Validate that all aggregate factories enforce non-empty GUIDs, trimmed non-empty strings, positive financial amounts, and valid future dates.
- **State Machine Integrity**:
  - `CampaignStatus`: Ensure valid transitions only (`Draft` -> `Published` -> `Successful`/`Failed`/`Cancelled`; publishing also requires an approved moderation review). Check that terminal states cannot be re-transitioned.
  - `ContributionStatus`: Ensure valid transitions only (`Pending` -> `Succeeded` / `Failed`, `Succeeded` -> `Refunded`). Verify that completed or failed contributions cannot be altered otherwise.
- **Exception Types**: Business rejections must throw `BusinessRuleViolationException` (400) or `DomainConflictException` (409), never `InvalidOperationException` or `ArgumentException`.
  - `CampaignReviewStatus`: Ensure review can only be approved/rejected once from `Pending`.
- **Validation Symmetry**: Verify that every business invariant checked in Domain constructors/methods is also checked in the corresponding FluentValidation command validator (e.g. string min/max lengths, regex formats, currency matches).

### 2. Multi-Currency & Financial Rules
- Verify that contributions enforce currency matching against the target campaign's currency.
- Test decimal precision and rounding rules (`MidpointRounding.AwayFromZero`, 2 decimal places).
- Check zero, negative, fractional, and overflow pledge amounts.

### 3. Cross-Module Domain Event Triggers
- When `Campaign.Create()` is invoked, verify `CampaignCreatedDomainEvent` is emitted.
- When `CampaignReview.Approve()` is called, verify `CampaignReviewApprovedDomainEvent` is emitted.
- When `Contribution.ConfirmPayment()` is called, verify `ContributionPaymentConfirmedDomainEvent` is emitted.
- Ensure handlers across modules consume these events and trigger expected state updates.

## Issue Reporting
When a domain invariant defect is identified:
1. Record it in your final report to the caller (file path and line, severity, violated rule). Do not create ticket files in the repository.
2. Document the aggregate, affected method, input values that break the invariant, and the resulting system state.
3. Recommend clean greenfield refactoring (no legacy compatibility required).

# Aggregates

## Purpose
Contains the aggregate roots that protect the core business rules for the Moderation module.

## Files
- `CampaignReview.cs`: Campaign review aggregate root: `Pending` -> `Approved` / `Rejected` by a moderator, plus `RecordAutomatedMediaAnalysis`, which stores safety scores and auto-approves a pending review that passed.

## Child Folders
- None.

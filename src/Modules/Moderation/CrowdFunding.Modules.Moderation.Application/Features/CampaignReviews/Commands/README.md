# Commands

## Purpose
Groups the command handlers of the Moderation module.

## Files
- No direct files live in this folder; it primarily organizes child folders.

## Child Folders
- `ApproveCampaignReview`: Implements the Approve Campaign Review command workflow for the Moderation module.
- `CreateCampaignReview`: Implements the Create Campaign Review command workflow for the Moderation module.
- `RecordMediaAnalysis`: Records the result of the asynchronous media-safety analysis delivered by the signed media-analysis webhook; a passing result auto-approves a pending review, otherwise the scores are stored for a human moderator.
- `RejectCampaignReview`: Implements the Reject Campaign Review command workflow for the Moderation module.

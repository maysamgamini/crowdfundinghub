# Commands

## Purpose
Groups the command handlers of the Campaigns module.

## Files
- No direct files live in this folder; it primarily organizes child folders.

## Child Folders
- `AddContributionToCampaign`: Implements the Add Contribution To Campaign command workflow for the Campaigns module.
- `CancelCampaign`: Implements the Cancel Campaign command workflow for the Campaigns module.
- `CompleteCampaign`: Moves a published campaign whose deadline passed with the goal reached to `Successful`; dispatched by `CampaignExpirationBackgroundService`.
- `CreateCampaign`: Implements the Create Campaign command workflow for the Campaigns module.
- `FailCampaign`: Moves a published campaign whose deadline passed without reaching the goal to `Failed`; dispatched by `CampaignExpirationBackgroundService`.
- `PublishCampaign`: Implements the Publish Campaign command workflow for the Campaigns module.
- `RecordContributionRefund`: Appends the compensating (negative) ledger entry for a refunded contribution.

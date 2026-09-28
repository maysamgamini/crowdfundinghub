# Get Campaign Review Status By Campaign Id

## Purpose
The cross-module query, result, and reader interface Campaigns uses to check a campaign's review status before publishing.

## Files
- `GetCampaignReviewStatusByCampaignIdQuery.cs`: Query model dispatched through the application messaging pipeline.
- `GetCampaignReviewStatusByCampaignIdResult.cs`: Result model returned by a handler or query operation.
- `ICampaignReviewStatusReader.cs`: Synchronous cross-module reader Campaigns calls before publishing to check that the campaign's review is approved; implemented in Moderation.Infrastructure.

## Child Folders
- None.

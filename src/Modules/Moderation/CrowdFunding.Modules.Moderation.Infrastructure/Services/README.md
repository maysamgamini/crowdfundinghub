# Services

## Purpose
Contains infrastructure service implementations used by the Moderation module.

## Files
- `CampaignReviewReadService.cs`: Read-model service used by query handlers to fetch denormalized data.
- `CampaignReviewStatusReader.cs`: Implements the `ICampaignReviewStatusReader` contract that Campaigns uses before publishing.
- `SystemDateTimeProvider.cs`: Implements `IModerationDateTimeProvider` with the system UTC clock.

## Child Folders
- None.

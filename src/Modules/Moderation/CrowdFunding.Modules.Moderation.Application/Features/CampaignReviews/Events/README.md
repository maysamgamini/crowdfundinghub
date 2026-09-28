# Events

## Purpose
Contains application event handlers that react to cross-module events relevant to the Moderation module.

## Files
- `CampaignCreatedApplicationEventHandler.cs`: Reacts to `CampaignCreatedApplicationEvent` from Campaigns by creating a pending `CampaignReview` (idempotent: an existing review is returned).

## Child Folders
- None.

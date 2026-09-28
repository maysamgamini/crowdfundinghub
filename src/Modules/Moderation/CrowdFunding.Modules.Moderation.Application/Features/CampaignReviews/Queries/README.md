# Queries

## Purpose
Groups the query handlers of the Moderation module.

## Files
- No direct files live in this folder; it primarily organizes child folders.

## Child Folders
- `GetCampaignReviewByCampaignId`: Implements the Get Campaign Review By Campaign Id query workflow for the Moderation module.
- `GetCampaignReviewStatusByCampaignId`: Handles the cross-module status query defined in `Moderation.Contracts`.
- `ListCampaignReviews`: The paged moderation queue, optionally filtered by status.

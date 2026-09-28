# Events

## Purpose
Groups event contracts or domain events used by the Campaigns module.

## Files
- No direct files live in this folder; it primarily organizes child folders.

## Child Folders
- `CampaignCancelled`: The application event published through the outbox when a campaign is cancelled.
- `CampaignCreated`: The application event published through the outbox when a campaign is created.
- `CampaignFailed`: `CampaignFailedApplicationEvent`, published when a campaign's deadline passes without reaching its goal; Contributions consumes it to refund every backer.
- `CampaignPublished`: The application event published through the outbox when a campaign is published.
- `CampaignSucceeded`: `CampaignSucceededApplicationEvent`, published when a campaign reaches its goal by its deadline.

# Moderation Module

## Purpose
The Moderation module governs content compliance and administrative review workflows, ensuring all campaigns meet platform policies and terms before public launch.

## Capabilities & Workflows
- **Automatic Review Provisioning**: Subscribes to `CampaignCreatedApplicationEvent` emitted by the Campaigns module to automatically initialize a `CampaignReview` in `Pending` status.
- **Review Decision Workflows**:
  - `ApproveCampaignReviewCommand`: Approves campaign submission, emits `CampaignReviewApprovedApplicationEvent`, allowing campaign creator to publish.
  - `RejectCampaignReviewCommand`: Rejects campaign with rejection notes and feedback, emitting `CampaignReviewRejectedApplicationEvent`.
- **Automated Media Analysis**: `RecordMediaAnalysisCommand`, dispatched by the signed media-analysis webhook (`/api/webhooks/moderation/media-analysis`), stores safety scores and auto-approves a pending review that passed.
- **Status Queries**: `ICampaignReviewStatusReader` (in Contracts) lets Campaigns check approval before publishing; `ListCampaignReviews` and `GetCampaignReviewByCampaignId` serve the moderation queue (`/api/moderation/reviews`, served by `ModerationController`).

## Projects & Layers
- `CrowdFunding.Modules.Moderation.Domain`: Contains `CampaignReview` aggregate root, `CampaignReviewStatus` enum, and review domain events.
- `CrowdFunding.Modules.Moderation.Application`: Implements review commands, query handlers, and event listeners.
- `CrowdFunding.Modules.Moderation.Infrastructure`: EF Core `ModerationDbContext`, configurations, repositories, the status reader, and the outbox processor.
- `CrowdFunding.Modules.Moderation.Contracts`: Cross-module review events, the review status query and reader, and `CampaignReviewStatusContract`.

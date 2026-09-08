# Moderation Module

## Purpose
The Moderation module governs content compliance and administrative review workflows, ensuring all campaigns meet platform policies and terms before public launch.

## Capabilities & Workflows
- **Automatic Review Provisioning**: Subscribes to `CampaignCreatedApplicationEvent` emitted by the Campaigns module to automatically initialize a `CampaignReview` in `Pending` status.
- **Review Decision Workflows**:
  - `ApproveCampaignReviewCommand`: Approves campaign submission, emits `CampaignReviewApprovedApplicationEvent`, allowing campaign creator to publish.
  - `RejectCampaignReviewCommand`: Rejects campaign with rejection notes and feedback, emitting `CampaignReviewRejectedApplicationEvent`.
- **Status Queries**: Exposes campaign review status checks consumed by creator dashboards and moderation queues.

## Projects & Layers
- `CrowdFunding.Modules.Moderation.Domain`: Contains `CampaignReview` aggregate root, `CampaignReviewStatus` enum, and review domain events.
- `CrowdFunding.Modules.Moderation.Application`: Implements review commands, query handlers, and event listeners.
- `CrowdFunding.Modules.Moderation.Infrastructure`: EF Core `ModerationDbContext`, configurations, repositories, and outbox tables.
- `CrowdFunding.Modules.Moderation.Contracts`: Cross-module review events and query contracts.

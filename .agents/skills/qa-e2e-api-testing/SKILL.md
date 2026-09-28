---
name: qa-e2e-api-testing
description: >-
  Use this skill to execute, trace, and audit end-to-end HTTP API request/response flows,
  route parameter bindings, status codes, endpoint orchestration, and integration test coverage.
---

# End-to-End API Calls & User Journey Audit Runbook

## Purpose
This skill guides the end-to-end verification of HTTP API calls across complete multi-step user journeys in the crowdfunding platform.

## End-to-End Journey Verification Checklist

### 1. User Journey 1: Creator Onboarding to Campaign Launch
1. `POST /api/identity/register`: Register new user. Validate response status (201) and payload.
2. `POST /api/identity/login`: Authenticate and receive JWT bearer token.
3. `POST /api/campaigns`: Submit campaign creation request with bearer token. Validate 201 Created and `Location` header.
4. `GET /api/campaigns/{id}`: Fetch created campaign. Validate status is `Draft`.
5. Background Event: Verify `CampaignCreatedDomainEvent` -> Outbox -> `Moderation` review created.

### 2. User Journey 2: Moderation Review to Publishing
1. `POST /api/identity/login` (as Moderator/Admin).
2. `GET /api/moderation/reviews/{campaignId}`: Check pending review.
3. `POST /api/moderation/reviews/{campaignId}/approve`: Approve campaign review.
4. `POST /api/campaigns/{id}/publish`: Publish campaign with creator token. Verify status becomes `Published`.

### 3. User Journey 3: Backer Pledging & Outbox Propagation
1. `POST /api/identity/register` & `login` (as Backer).
2. `POST /api/campaigns/{campaignId}/contributions`: Submit pledge. Verify status is `Pending`.
3. `POST /api/campaigns/{campaignId}/contributions/{contributionId}/confirm-payment`: Confirm payment.
4. Verify Outbox background worker picks up `ContributionPaymentConfirmedApplicationEvent`.
5. Verify a pledge entry is appended to the campaign ledger, `Campaign.RaisedAmount` equals the ledger sum, and a `PledgeReceived` SignalR message is sent to the campaign group (`/hubs/campaigns`).
6. Optionally drive the payment through `POST /api/webhooks/payments/stripe` (signed `Stripe-Signature` header) instead of the confirm-payment endpoint, and verify a redelivered event is ignored.

### 4. End-to-End API Test Suite Evaluation
- Audit `tests/IntegrationTests/CrowdFunding.IntegrationTests` (`CrowdFundingApiFactory`, a `WebApplicationFactory<Program>` over PostgreSQL and Redis Testcontainers, with `ProcessOutboxMessagesAsync` to drain outboxes deterministically) for tests executing these complete workflows.
- Identify missing end-to-end test fixtures and integration tests.

## Reporting
Report any broken API links, route binding defects, or missing E2E integration test suites in your final report to the caller. Do not create ticket files in the repository.

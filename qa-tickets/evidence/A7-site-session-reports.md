# Findings reported by the site session (hicleanarch) about the reference repo docs

Source: cross-session message from the site session after aligning Stage 2 with reference main. Each item is a mismatch in the crowdfunding repo to fix there. Not yet verified by me line by line; verify when ticketing.

1. educational/03-engineering-spec-and-roadmap/deconstruction-walkthrough.md shows JWKS configured via an authority address; services/CrowdFunding.Moderation.Service actually uses JwksSigningKeyResolver.
2. services/CrowdFunding.Moderation.Service/README.md has a stale "Jwks" settings section.
3. Essays name types that do not exist: CampaignCreatedHandler (real: CampaignCreatedApplicationEventHandler), InMemoryMessageBus (real: InProcessMessageBus), TargetAmount (real: GoalAmount), campaign_read_models (real: active_campaigns_cache).
4. over-engineering-audit.md: "400+ lines of crypto" (real: 148 + 42), "29 projects" (real: 27 under src).
5. MultiDatabaseConnectionDecouplingTests covers Campaigns, not Moderation, though docs say otherwise.
6. RabbitMqMessageBus only publishes; there is no consumer; docs imply a full bus.
7. docs/EVENT_DELIVERY_APPROACHES.md quotes 5.04 s and "under one second"; the test asserts under 1.5 s and the numbers come from a local run, so the doc must say so. The guide lists six approaches (A to F); my message to the site session said seven.
8. Design rule "one live pledge per backer per campaign" is still missing in the reference code (site marks the double-click entry "Partly built"). Candidate new ticket / next build item.

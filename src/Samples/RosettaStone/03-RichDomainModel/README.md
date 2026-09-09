# Tier 3: Rich Domain Model + Outbox

There is no code in this folder. Tier 3's implementation of "create a campaign" **is** the
monolith's real, production `POST /api/campaigns` endpoint — copying it here would create a
second, drifting source of truth. Read the real pipeline instead:

| Step | File |
| :--- | :--- |
| HTTP entry point | [`CampaignsController.Create`](../../../API/CrowdFunding.API/Controllers/CampaignsController.cs) |
| Request contract | [`CreateCampaignRequest`](../../../API/CrowdFunding.API/Contracts/Campaigns/CreateCampaignRequest.cs) |
| Command | [`CreateCampaignCommand`](../../../Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/Campaigns/Commands/CreateCampaign/CreateCampaignCommand.cs) |
| Validator | [`CreateCampaignCommandValidator`](../../../Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/Campaigns/Commands/CreateCampaign/CreateCampaignCommandValidator.cs) |
| Handler | [`CreateCampaignCommandHandler`](../../../Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/Campaigns/Commands/CreateCampaign/CreateCampaignCommandHandler.cs) |
| Domain aggregate factory | [`Campaign.Create`](../../../Modules/Campaigns/CrowdFunding.Modules.Campaigns.Domain/Aggregates/Campaign.cs) |
| Domain event | [`CampaignCreatedDomainEvent`](../../../Modules/Campaigns/CrowdFunding.Modules.Campaigns.Domain/Events/CampaignCreatedDomainEvent.cs) |
| Application event (outbox payload) | [`CampaignCreatedApplicationEvent`](../../../Modules/Campaigns/CrowdFunding.Modules.Campaigns.Contracts/Events/CampaignCreated/CampaignCreatedApplicationEvent.cs) |
| Repository | [`CampaignRepository`](../../../Modules/Campaigns/CrowdFunding.Modules.Campaigns.Infrastructure/Persistence/Repositories/CampaignRepository.cs) |
| Transaction + outbox executor | [`CampaignTransactionExecutor`](../../../Modules/Campaigns/CrowdFunding.Modules.Campaigns.Infrastructure/Transactions/CampaignTransactionExecutor.cs) |
| Response contract | [`CreateCampaignResponse`](../../../API/CrowdFunding.API/Contracts/Campaigns/CreateCampaignResponse.cs) |

That's 14 files. Several of them (the controller, the aggregate, the repository, the
transaction executor) are *shared* across every Campaigns write use case — Publish, Cancel,
AddContribution, etc. — so the marginal cost of one more command on top of an already-justified
Tier 3 module is smaller than the raw count suggests. It's the *first* command that pays for the
aggregate, the outbox executor, and the transaction boundary; see the root `README.md`
scorecard for the honest line-count breakdown of what's genuinely Create-specific versus
shared infrastructure this use case rides on for free.

## Why this tier earns its cost here, but wouldn't for a lookup table

`Campaign` is the aggregate that will, in later commands (`AddContribution`, `Publish`,
`Cancel`), enforce invariants that a flat `CampaignRecord` insert cannot: a campaign can't
receive contributions once cancelled, funding totals can't be corrupted by concurrent writers
(`xmin` optimistic concurrency), and every state transition emits a domain event that other
modules (Notifications, Moderation, CampaignUpdates) rely on being delivered at-least-once via
the outbox — even across a process crash between the SQL commit and the message being
published. None of that is exercised by "insert a title and a target amount," which is exactly
why Tier 1 and Tier 2 in this sample skip it.

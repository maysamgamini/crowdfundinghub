# Crowd Funding Modules Campaigns Contracts

## Purpose
Contains integration-facing contracts for the Campaigns module, such as cross-module commands, queries, and events.

## Files
- `CrowdFunding.Modules.Campaigns.Contracts.csproj`: Project file that defines dependencies, target framework, and assembly references for this area.

## Child Folders
- `Commands`: Groups contract-level commands exposed by the Campaigns module.
- `Enums`: `CampaignStatusContract`, the cross-module mirror of the domain `CampaignStatus`.
- `Events`: Groups event contracts or domain events used by the Campaigns module.
- `Queries`: Currently empty (only its README); the Campaigns module exposes no cross-module queries. Contributions validates pledges against its own replicated campaign snapshot instead.

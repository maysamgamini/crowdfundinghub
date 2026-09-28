# src

## Purpose
Contains all production code for the solution, including the API host, shared building blocks, and feature modules.

## Files
- No direct files live in this folder; it primarily organizes child folders.

## Child Folders
- `API`: Groups the HTTP API host for the solution.
- `BuildingBlocks`: Holds reusable cross-cutting abstractions shared by multiple modules.
- `Modules`: Contains the six feature modules (Identity, Campaigns, Contributions, Moderation, CampaignUpdates, Notifications) that implement the business capabilities of the platform.
- `Samples`: Teaching samples that run as their own hosts and are never referenced by the API (currently `RosettaStone`, see [`Samples/RosettaStone/README.md`](Samples/RosettaStone/README.md)).

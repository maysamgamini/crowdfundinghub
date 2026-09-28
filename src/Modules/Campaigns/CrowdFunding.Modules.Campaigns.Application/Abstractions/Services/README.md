# Services

## Purpose
Defines service contracts that the Campaigns application layer relies on for time, reads, or externalized behavior.

## Files
- `ICampaignReadService.cs`: Read-model service used by query handlers to fetch denormalized data.
- `ICampaignRealtimeNotifier.cs`: Pushes live funding updates (raised total, sold-out reward tiers) to connected clients; implemented by the API host over SignalR.
- `IDateTimeProvider.cs`: Abstraction or implementation for retrieving the current UTC time.

## Child Folders
- None.

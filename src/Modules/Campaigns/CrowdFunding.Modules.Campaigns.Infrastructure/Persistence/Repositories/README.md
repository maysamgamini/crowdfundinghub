# Repositories

## Purpose
Contains repository implementations for the Campaigns module.

## Files
- `CampaignLedger.cs`: Implements `ICampaignLedger`: inserts entries with `INSERT ... ON CONFLICT DO NOTHING` (a refund only when its pledge exists) and recomputes `campaigns.raised_amount` as `SUM(entries)`.
- `CampaignRepository.cs`: Loads and persists `Campaign` aggregates and enqueues post-commit cache eviction.
- `RewardTierRepository.cs`: Loads and persists `RewardTier` aggregates.
- `RewardTierReservationRepository.cs`: Loads and persists reward-tier reservations, including the expired ones the scavenger releases.

## Child Folders
- None.

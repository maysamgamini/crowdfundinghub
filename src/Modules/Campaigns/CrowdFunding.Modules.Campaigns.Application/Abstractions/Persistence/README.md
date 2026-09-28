# Persistence

## Purpose
Defines persistence ports consumed by the Campaigns application layer so handlers stay independent from EF Core details.

## Files
- `ICampaignLedger.cs`: Port for the append-only funding ledger: appends pledge and refund entries idempotently and recomputes the raised-amount snapshot.
- `ICampaignRepository.cs`: Repository port that loads and persists `Campaign` aggregates.
- `IRewardTierRepository.cs`: Repository port for `RewardTier` aggregates.
- `IRewardTierReservationRepository.cs`: Repository port for reward-tier slot reservations (including finding expired ones).

## Child Folders
- None.

# Events

## Purpose
Contains application event handlers that react to cross-module events relevant to the Contributions module.

## Files
- `CampaignTerminationRefundHandler.cs`: The refund saga (event choreography, no distributed transaction). Reacts to `CampaignFailedApplicationEvent` and `CampaignCancelledApplicationEvent` by refunding every succeeded contribution of the campaign in batches, each refund raising `ContributionRefundedApplicationEvent`.

## Child Folders
- None.

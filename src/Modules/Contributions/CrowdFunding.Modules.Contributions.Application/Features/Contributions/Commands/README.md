# Commands

## Purpose
Groups the command use cases of the Contributions module.

## Files
- No direct files live in this folder; it primarily organizes child folders.

## Child Folders
- `ConfirmContributionPayment`: Implements the Confirm Contribution Payment command workflow for the Contributions module.
- `FailContributionPayment`: Implements the Fail Contribution Payment command workflow for the Contributions module.
- `MakeContribution`: Implements the Make Contribution command workflow for the Contributions module.
- `ReconcilePaymentWebhook`: Applies a signature-verified payment-gateway webhook event to its contribution, exactly once per gateway event id (via the inbox).

# Aggregates

## Purpose
Contains the aggregate roots that protect the core business rules for the Contributions module.

## Files
- `Contribution.cs`: The contribution aggregate root. Enforces the payment-state transitions (`AttachPaymentIntent`, `ConfirmPayment`, `FailPayment`, `Refund`) and raises the matching domain events.

## Child Folders
- None.

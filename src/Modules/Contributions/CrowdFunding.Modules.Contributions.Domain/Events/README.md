# Events

## Purpose
Contains the domain events raised by the `Contribution` aggregate.

## Files
- `ContributionPaymentConfirmedDomainEvent.cs`: Raised when a payment is confirmed.
- `ContributionPaymentFailedDomainEvent.cs`: Raised when a payment fails; carries the reward-tier reservation id so the reservation is released immediately.
- `ContributionRefundedDomainEvent.cs`: Raised when a contribution is refunded by the refund saga.

## Child Folders
- None.

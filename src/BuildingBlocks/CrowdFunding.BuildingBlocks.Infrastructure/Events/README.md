# Events

## Purpose
Contains infrastructure implementations that resolve and invoke application event handlers.

## Files
- `ServiceProviderEventPublisher.cs`: Event publisher that resolves registered handlers from the service provider, applies the inbox, and skips a handler another worker already recorded.
- `InboxRegistration.cs`: Ties a module's handlers to its inbox table and registers the in-transaction recorder (`InboxRecorder<TDbContext>`, in `Persistence`) that a module's transaction executor calls for handlers marked `IRecordsInboxInTransaction`.

## Child Folders
- None.

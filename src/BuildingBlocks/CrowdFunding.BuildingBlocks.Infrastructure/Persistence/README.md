# Persistence

## Purpose
Contains shared persistence helpers such as outbox storage and EF Core model configuration extensions.

## Files
- `DomainEventAccessor.cs`: Supporting source file for Domain Event Accessor.
- `ModelBuilderExtensions.cs`: Extension methods that encapsulate repeated registration or configuration logic.
- `OutboxMessage.cs`: Persistent outbox record used to safely publish application events after database commits.

## Child Folders
- None.

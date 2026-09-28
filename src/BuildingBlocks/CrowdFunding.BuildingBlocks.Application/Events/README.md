# Events

## Purpose
Contains shared abstractions for publishing and handling application events.

## Files
- `BaseApplicationEvent.cs`: Base class for application events published for cross-module reactions. Its `EventId` is stable across serialization and becomes the outbox row id and the CloudEvents `id`.
- `EventTypeRegistry.cs`: Maps an event type name and version to a CLR type, so the outbox processor can deserialize stored payloads.
- `EventVersionAttribute.cs`: Declares the schema version of an application event (read by `EventTypeRegistry`).
- `IEventHandler.cs`: Contract for a handler that reacts to an application event.
- `IEventPublisher.cs`: Contract for publishing an event to every registered in-process handler.
- `NotificationRegistrationExtensions.cs`: `AddEventHandlersFromAssemblies` and `AddEventTypeRegistry` registration helpers.

## Child Folders
- None.

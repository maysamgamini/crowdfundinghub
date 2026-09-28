# Common

## Purpose
Contains base domain building blocks used across aggregates.

## Files
- `BaseEntity.cs`: Base aggregate or entity type that captures shared domain-event behavior.
- `BaseEvent.cs`: Base domain event type.
- `AdvisoryLockKey.cs`: Derives a deterministic 64-bit PostgreSQL advisory lock key from a GUID (`FromGuid`), used with `pg_advisory_xact_lock`.

## Child Folders
- None.

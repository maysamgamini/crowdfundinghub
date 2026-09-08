# Building Blocks: Domain Layer

## Purpose
Provides enterprise domain building blocks, pure business abstractions, shared immutable value objects, domain event foundations, and database-agnostic locking key utilities. Completely free of external or infrastructure dependencies.

## Subdirectories
- `Common`:
  - `BaseEntity.cs`: Abstract base class managing local domain event collections (`IReadOnlyCollection<BaseEvent>`), event registration (`AddDomainEvent`), and clearing (`ClearDomainEvents`).
  - `BaseEvent.cs`: Base record for all domain events, containing an immutable event ID and UTC occurrence timestamp.
  - `AdvisoryLockKey.cs`: Deterministic 64-bit integer generator derived from GUIDs (`FromGuid`), used for application-level PostgreSQL transaction advisory locking (`pg_advisory_xact_lock`) to prevent race conditions during concurrent financial operations.
- `ValueObjects`:
  - `Money.cs`: Strongly typed, immutable monetary value object encapsulating decimal amount and ISO currency code (e.g. `USD`). Enforces invariant rules including non-negative amounts, zero-construction (`Money.Zero`), and currency matching across mathematical operations (`Add`, `Subtract`).

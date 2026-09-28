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
- `Exceptions`:
  - `DomainException.cs`: Abstract base for every business rejection a domain model throws. Its message is client-safe and becomes the ProblemDetails `detail`.
  - `BusinessRuleViolationException.cs`: The input breaks an invariant whatever the aggregate's state (blank title, negative amount, currency mismatch). Mapped to HTTP 400.
  - `DomainConflictException.cs`: The operation is not allowed in the aggregate's current state (publishing a non-draft campaign, reserving a sold-out tier). Mapped to HTTP 409.

## Why domain code never throws `InvalidOperationException` or `ArgumentException`
EF Core, Npgsql and the framework throw those BCL types too, and their messages can name tables or connection settings. The API maps only exception types this codebase owns to 4xx and turns everything else into a generic 500 (see `BuildingBlocks.Infrastructure/ErrorHandling/ExceptionClassifier.cs`), so business rejections must use a `DomainException` subtype.

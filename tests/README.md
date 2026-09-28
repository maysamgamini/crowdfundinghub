# Automated Test Suite

## Purpose
Contains automated test suites designed to safeguard software correctness, enforce architectural boundaries, verify high-concurrency invariants, and prevent regressions across the codebase.

## Test Strategy & Categories
1. **Architecture Tests (`tests/ArchitectureTests`)**:
   - Plain xUnit tests that inspect compiled assembly references (`Assembly.GetReferencedAssemblies()`); no third-party rules library.
   - Verifies that `API` does not bypass application boundaries to reference domain models directly, references no teaching sample, and keeps exactly one controller.
   - Asserts that every module has the four layers and that modules only communicate via `Contracts`, never through each other's Domain, Application, or Infrastructure assemblies.
2. **Unit Tests (`tests/UnitTests`)**:
   - Isolated, in-memory tests running in milliseconds.
   - Uses lightweight fakes (`FakeTransactionExecutors`, `TestCurrentUser`, `TestDispatchers`) instead of database connections or network sockets.
   - Tests business rules, aggregate state transitions, command validators, CQRS handlers, the command pipeline (validation and audit behaviors), exception classification, outbox serialization and wake-up signal, webhook signature verification, signing-key encryption, and OpenMeter CloudEvent mapping.
3. **Integration Tests (`tests/IntegrationTests`)**:
   - Database-backed integration tests utilizing real PostgreSQL, Redis, and RabbitMQ instances via `Testcontainers`.
   - Exercises the full HTTP pipeline end to end, database migrations, the append-only campaign ledger under concurrency, optimistic concurrency, outbox delivery and wake-up, inbox idempotency, webhooks, and edge hardening (rate limiting, CORS).

## Running Tests
- **Run all tests**:
  ```bash
  dotnet test CrowdFunding.slnx
  ```
- **Run fast unit tests only**:
  ```bash
  dotnet test tests/UnitTests/CrowdFunding.UnitTests
  ```
- **Run architecture tests**:
  ```bash
  dotnet test tests/ArchitectureTests/CrowdFunding.ArchitectureTests
  ```
- **Run integration tests (requires Docker)**:
  ```bash
  dotnet test tests/IntegrationTests/CrowdFunding.IntegrationTests
  ```

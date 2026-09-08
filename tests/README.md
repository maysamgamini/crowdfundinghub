# Automated Test Suite

## Purpose
Contains automated test suites designed to safeguard software correctness, enforce architectural boundaries, verify high-concurrency invariants, and prevent regressions across the codebase.

## Test Strategy & Categories
1. **Architecture Tests (`tests/ArchitectureTests`)**:
   - Uses `NetArchTest.Rules` to enforce architectural constraints.
   - Verifies that `API` does not bypass application boundaries to reference domain models directly.
   - Asserts that modules only communicate via `Contracts` and cannot depend on each other's internal domain or infrastructure implementations.
2. **Unit Tests (`tests/UnitTests`)**:
   - Isolated, in-memory tests running in milliseconds.
   - Uses lightweight fakes (`FakeTransactionExecutors`, `TestCurrentUser`, `TestDispatchers`) instead of database connections or network sockets.
   - Tests business rules, aggregate state transitions, command validators, CQRS handlers, outbox serialization, and OpenMeter CloudEvent mapping.
3. **Integration Tests (`tests/IntegrationTests`)**:
   - Database-backed integration tests utilizing real PostgreSQL instances via `Testcontainers`.
   - Exercises end-to-end database migrations, advisory lock concurrency, race conditions during parallel contributions, and outbox idempotency.

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

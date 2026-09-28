# Dependency Injection

## Purpose
Registers the Contributions infrastructure (DbContext with its per-module connection string and migration history schema, repositories, inbox consumer, read service, transaction executor, clock, and outbox worker) with the dependency injection container.

## Files
- `ContributionsInfrastructureDependencyInjection.cs`: Registration entry point for the Contributions infrastructure layer.

## Child Folders
- None.

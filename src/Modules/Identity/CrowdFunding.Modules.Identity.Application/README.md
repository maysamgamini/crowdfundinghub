# Identity: Application Layer

## Purpose
Orchestrates authentication and user management workflows, including self-registration, credential validation, current-user querying, and out-of-band administrator account provisioning.

## Subdirectories
- `Abstractions`: Persistence abstractions (`IUserRepository`), transaction executors (`IIdentityTransactionExecutor`), token services (`IJwtTokenService`), password hasher (`IPasswordHasher`), and time providers (`IIdentityDateTimeProvider`).
- `DependencyInjection`: Extension methods (`AddIdentityApplication`) registering handlers with the DI container.
- `Features`:
  - `Users/Commands/RegisterUser`: Handles new user self-registration with password hashing and default `User` role assignment.
  - `Users/Commands/Login`: Validates email/password credentials and issues asymmetric ES256 JWT access tokens.
  - `Users/Commands/SeedAdmin`: Out-of-band CLI handler for creating or promoting the platform administrator.
  - `Users/Queries/GetCurrentUser`: Queries profile details for the authenticated user based on claims context.

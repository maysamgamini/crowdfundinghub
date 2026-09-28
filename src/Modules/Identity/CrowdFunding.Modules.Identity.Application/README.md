# Identity: Application Layer

## Purpose
Orchestrates authentication and user management workflows, including self-registration, credential validation, refresh-token rotation, logout, role and permission administration, account deactivation, current-user querying, and out-of-band administrator account provisioning.

## Subdirectories
- `Abstractions`: Persistence abstractions (`IUserRepository`, `IRefreshTokenRepository`), the transaction executor (`IIdentityTransactionExecutor`), token services (`IAccessTokenProvider`, `IRefreshTokenService`, `ISigningKeyStore`), the revocation list (`ISecurityStampRevocationStore`), password hasher (`IPasswordHasher`), and time provider (`IIdentityDateTimeProvider`).
- `DependencyInjection`: Extension method (`AddIdentityApplication`) registering handlers and validators with the DI container.
- `Features`:
  - `Users/Commands/RegisterUser`: Handles new user self-registration with password hashing and default `Creator` and `Backer` role assignment.
  - `Users/Commands/LoginUser`: Validates email/password credentials and issues an ES256 JWT access token plus a refresh token.
  - `Users/Commands/RefreshAccessToken`: Refresh Token Rotation with reuse detection.
  - `Users/Commands/Logout`: Revokes the refresh token and rotates the security stamp.
  - `Users/Commands/AssignRoleToUser` / `GrantPermissionToUser`: Administrative role and permission grants.
  - `Users/Commands/DeactivateUser`: Suspends an account and revokes its tokens instantly.
  - `Users/Commands/SeedAdmin`: Out-of-band CLI handler for creating or promoting the platform administrator.
  - `Users/Queries/GetCurrentUser`: Returns profile, roles, and effective permissions for the authenticated user (identified by the `sub` claim through `ICurrentUser`).

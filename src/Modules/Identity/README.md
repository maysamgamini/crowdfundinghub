# Identity Module

## Purpose
The Identity module manages user registration, password hashing, credential authentication, asymmetric ECDSA (ES256) JSON Web Token (JWT) issuance, refresh-token rotation, token revocation, JWKS signing-key management, and role-based access control (RBAC).

## Capabilities & Workflows
- **User Self-Registration**: Public endpoint creates accounts with normalized email and assigns the `Creator` and `Backer` roles (`RegisterUserCommand`). Self-registration never grants `Admin`, not even to the first user.
- **Administrative Provisioning**: Out-of-band CLI runner creates or promotes the initial `Admin` user (`SeedAdminCommand`); it is the only code path that grants `Admin`.
- **Asymmetric Authentication**: Generates ES256 JWT access tokens signed with ECDSA private keys. The caller's identity is carried in the `sub` claim, alongside `email`, `display_name`, `security_stamp`, and one `role` / `permission` claim per role and permission (names in `CustomClaimTypes`).
- **Refresh Token Rotation and Revocation**: Login returns an access token and an opaque refresh token (only its SHA-256 hash is stored in `refresh_tokens`). Each refresh revokes the presented token and issues a new pair; reusing an already-rotated token revokes every session of the account. Logout and deactivation rotate the user's `SecurityStamp` and publish the old stamp to a Redis revocation list, so an unexpired access token stops working immediately.
- **Signing Key Management**: Stores ES256 signing keys in the database (`identity_signing_keys`), with private keys encrypted at rest by `SigningKeyProtector` (AES-256-GCM, key from `SigningKeyEncryption:Key`; startup fails without it). Keys are cached in memory, refreshed across instances via Redis Pub/Sub and a periodic fallback, and can be rotated (`ISigningKeyStore.RotateAsync`) while old public keys stay in the JWKS.
- **Role & Permission Mapping**: Maps roles (`Admin`, `Moderator`, `Creator`, `Backer`) to granular permissions (`campaigns.create`, `campaigns.publish`, `campaigns.contribute`, `moderation.review`, etc., defined in `PermissionConstants`) via `RolePermissionCatalog`. Administrators can also grant individual permissions directly.

## Projects & Layers
- `CrowdFunding.Modules.Identity.Domain`: Contains the `User` aggregate root (derives from `BaseEntity`) and its child entities `UserRoleAssignment`, `UserPermissionGrant`, and `RefreshToken`. The module raises no domain events.
- `CrowdFunding.Modules.Identity.Application`: Implements registration, login, refresh, logout, role/permission administration, deactivation, admin seeding, and the current-user query.
- `CrowdFunding.Modules.Identity.Infrastructure`: EF Core `IdentityDbContext`, the PBKDF2 password hasher, `EfSigningKeyStore`, `SigningKeyProtector`, `JwtAccessTokenProvider`, the refresh-token service, and the Redis revocation store. Every table and the migration history live in the `identity` schema.
- `CrowdFunding.Modules.Identity.Contracts`: Authorization constants (`RoleConstants`, `PermissionConstants`, `CustomClaimTypes`, `RolePermissionCatalog`).

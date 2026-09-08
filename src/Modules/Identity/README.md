# Identity Module

## Purpose
The Identity module manages user registration, password hashing, credential authentication, asymmetric ECDSA (ES256) JSON Web Token (JWT) issuance, dynamic JWKS key rotation, and role-based access control (RBAC).

## Capabilities & Workflows
- **User Self-Registration**: Public endpoint creates accounts with normalized email and assigns standard `User` role (`RegisterUserCommand`).
- **Administrative Provisioning**: Out-of-band CLI runner creates initial `Admin` user (`SeedAdminCommand`).
- **Asymmetric Authentication**: Generates ES256 JWT access tokens signed with ECDSA private keys.
- **Dynamic Key Management**: Stores private and public signing keys in the database (`identity_signing_keys`), supporting seamless zero-downtime key rotation.
- **Role & Permission Mapping**: Maps roles (`Admin`, `Moderator`, `User`) to granular permissions (`Campaigns.Create`, `Campaigns.Publish`, `Reviews.Approve`, etc.) via `RolePermissionCatalog`.

## Projects & Layers
- `CrowdFunding.Modules.Identity.Domain`: Contains the `User` aggregate root, `UserRole` child entity, and domain events.
- `CrowdFunding.Modules.Identity.Application`: Implements login, registration, and user query handlers.
- `CrowdFunding.Modules.Identity.Infrastructure`: EF Core `IdentityDbContext`, password hasher (PBKDF2/Argon2), `DatabaseSigningKeyStore`, and token generators.
- `CrowdFunding.Modules.Identity.Contracts`: Authorization constants (`RoleConstants`, `PermissionConstants`, `CustomClaimTypes`).

# Dependency Injection

## Purpose
Registers Identity infrastructure components with the dependency injection container.

## Files
- `IdentityInfrastructureDependencyInjection.cs`: `AddIdentityInfrastructure` registers `IdentityDbContext` (connection `ConnectionStrings:IdentityDb`, falling back to `DefaultConnection`; migration history in the `identity` schema), repositories, the transaction executor, token and password services, the Redis revocation store, `SigningKeyProtector`, `EfSigningKeyStore`, and `SigningKeyRefreshBackgroundService`.

## Child Folders
- None.

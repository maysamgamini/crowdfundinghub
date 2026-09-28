# Services

## Purpose
Contains infrastructure service implementations used by the Identity module.

## Files
- `EfSigningKeyStore.cs`: `ISigningKeyStore` over `IdentityDbContext`; keeps keys in memory after `WarmUpAsync`, rotates keys, and publishes a Redis Pub/Sub invalidation so other instances reload.
- `JwtAccessTokenProvider.cs`: Creates ES256-signed JWT access tokens (claims from `CustomClaimTypes`, `kid` header from the active signing key).
- `JwtOptions.cs`: Configuration model for JWT token generation (issuer, audience, access-token lifetime in minutes, refresh-token lifetime in days).
- `Pbkdf2PasswordHasher.cs`: Hashes and verifies passwords using PBKDF2 (SHA-256, random salt, iteration count stored with the hash).
- `RedisSecurityStampRevocationStore.cs`: `ISecurityStampRevocationStore` backed by Redis; a revoked stamp is kept for the access-token lifetime.
- `RefreshTokenService.cs`: Generates 256-bit random refresh tokens and hashes them with SHA-256 for storage.
- `SigningKeyProtector.cs`: Encrypts ES256 private keys at rest with AES-256-GCM, binding each ciphertext to its key id; the key comes from `SigningKeyEncryption:Key` and startup fails without it. Legacy plaintext values are still readable so they can be re-encrypted.
- `SigningKeyRefreshBackgroundService.cs`: Re-runs the signing key warm-up on a fixed interval, the fallback when a Pub/Sub invalidation is missed.
- `SystemDateTimeProvider.cs`: Provides the current UTC time (`IIdentityDateTimeProvider`).

## Child Folders
- None.

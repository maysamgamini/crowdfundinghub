# Services

## Purpose
Defines service contracts that the Identity application layer relies on for time, tokens, signing keys, password hashing, and revocation.

## Files
- `AccessToken.cs`: A generated access token together with its expiration timestamp.
- `IAccessTokenProvider.cs`: Creates signed access tokens for authenticated users.
- `IIdentityDateTimeProvider.cs`: Provides the current UTC time to the Identity module.
- `IPasswordHasher.cs`: Hashes and verifies user passwords.
- `IRefreshTokenService.cs`: Generates opaque refresh tokens and hashes them; only the hash is persisted.
- `ISecurityStampRevocationStore.cs`: Distributed revocation list of security stamps (`RevokeAsync`, `IsRevokedAsync`), checked on every authenticated request so logout, reuse detection, and deactivation invalidate unexpired access tokens.
- `ISigningKeyStore.cs`: ES256 signing key store (`WarmUpAsync`, `GetActiveSigningKey`, `GetPublicSigningKeys`, `RotateAsync`) and the `SigningKeyMaterial` record (key id plus ECDSA key).

## Child Folders
- None.

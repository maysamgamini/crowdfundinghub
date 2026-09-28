# Configurations

## Purpose
Contains EF Core entity configuration classes for the Identity module.

## Files
- `RefreshTokenConfiguration.cs`: Maps `RefreshToken` to `refresh_tokens` (unique token hash, concurrency token).
- `SigningKeyRecordConfiguration.cs`: Maps `SigningKeyRecord` to `identity_signing_keys`.
- `UserConfiguration.cs`: Maps `User` to `users`, with its roles in `user_roles` and direct permissions in `user_permissions`.

## Child Folders
- None.

# Migrations

## Purpose
Contains the EF Core migration history for the Identity module. These files are generated snapshots of schema evolution. The migration history table is `identity.__EFMigrationsHistory`; the module's tables are in the `public` schema.

## Files
- `20260407051933_InitialIdentitySchema.cs` (+ `.Designer.cs`): Users, roles, and permissions tables.
- `20260908213230_AddSigningKeys.cs` (+ `.Designer.cs`): The `identity_signing_keys` table.
- `20260909034804_AddRefreshTokensAndSecurityStamp.cs` (+ `.Designer.cs`): The `refresh_tokens` table and the users' security stamp.
- `20260909153329_AddRefreshTokenConcurrencyToken.cs` (+ `.Designer.cs`): Concurrency token on refresh tokens.
- `20260928165938_EncryptSigningKeysAtRest.cs` (+ `.Designer.cs`): Renames `PrivateKeyPkcs8Base64` to `ProtectedPrivateKey`; existing plaintext values are re-encrypted by the next signing key warm-up.
- `IdentityDbContextModelSnapshot.cs`: Current model snapshot.

## Child Folders
- None.

## Notes
Avoid hand-editing migration designer files unless you are intentionally repairing generated output.

# Entities

## Purpose
Contains supporting entities that belong to the Identity domain model.

## Files
- `RefreshToken.cs`: One refresh token in a rotation chain. Only the SHA-256 `TokenHash` is stored; tracks expiry, revocation time, and the hash of the token that replaced it.
- `UserPermissionGrant.cs`: A permission granted directly to a user.
- `UserRoleAssignment.cs`: A role assigned to a user.

## Child Folders
- None.

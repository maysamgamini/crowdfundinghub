# Authorization

## Purpose
Contains shared authorization constants and claim metadata for the identity model.

## Files
- `CustomClaimTypes.cs`: Claim names written into access tokens and read back (`sub`, `email`, `role`, `display_name`, `permission`, `security_stamp`). Inbound claim mapping is off, so these are the exact names on the wire; the user id is read only from `sub`.
- `PermissionConstants.cs`: Permission names (`campaigns.create`, `campaigns.publish`, `campaigns.cancel`, `campaigns.contribute`, `contributions.payments.manage`, `campaigns.manage.any`, `moderation.review`, `identity.roles.assign`, `identity.permissions.grant`, `users.manage`) and the `All` collection.
- `RoleConstants.cs`: Built-in role names (`Admin`, `Moderator`, `Creator`, `Backer`).
- `RolePermissionCatalog.cs`: Maps each built-in role to the permissions it grants (`Admin` gets all of them).

## Child Folders
- None.

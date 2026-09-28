# Users

## Purpose
Groups identity user features by command and query use case.

## Files
- `UserAuthorizationProjection.cs`: Internal helper that extracts a user's roles, explicit permissions, and effective permissions (role permissions from `RolePermissionCatalog` plus direct grants) for handler results.

## Child Folders
- `Commands`: Groups contract-level commands exposed by the Identity module.
- `Queries`: Groups contract-level queries exposed by the Identity module.

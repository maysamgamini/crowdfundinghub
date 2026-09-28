# Commands

## Purpose
Groups the command use cases of the Identity module.

## Files
- No direct files live in this folder; it primarily organizes child folders.

## Child Folders
- `AssignRoleToUser`: Implements the Assign Role To User command workflow for the Identity module.
- `DeactivateUser`: Suspends a user account, rotates its security stamp, and revokes its refresh tokens.
- `GrantPermissionToUser`: Implements the Grant Permission To User command workflow for the Identity module.
- `LoginUser`: Implements the Login User command workflow for the Identity module.
- `Logout`: Revokes the presented refresh token and rotates the caller's security stamp.
- `RefreshAccessToken`: Refresh Token Rotation with reuse detection.
- `RegisterUser`: Implements the Register User command workflow for the Identity module.
- `SeedAdmin`: Creates or promotes the initial administrator; dispatched only from the CLI.

# Aggregates

## Purpose
Contains the aggregate roots that protect the core business rules for the Identity module.

## Files
- `User.cs`: The user aggregate root (derives from `BaseEntity`): normalized email and display name, password hash, active flag, `SecurityStamp`, roles, and direct permission grants. Deactivation rotates the security stamp.

## Child Folders
- None.

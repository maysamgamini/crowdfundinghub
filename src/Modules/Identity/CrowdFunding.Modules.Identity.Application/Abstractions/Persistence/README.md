# Persistence

## Purpose
Defines persistence ports consumed by the Identity application layer so handlers stay independent from EF Core details.

## Files
- `IRefreshTokenRepository.cs`: Persistence port for refresh tokens (add, find by token hash, list a user's active tokens, update).
- `IUserRepository.cs`: Persistence port for the `User` aggregate.

## Child Folders
- None.

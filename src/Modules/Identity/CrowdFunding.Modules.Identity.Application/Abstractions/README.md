# Abstractions

## Purpose
Contains the ports (interfaces and small models) the Identity application layer depends on; Identity.Infrastructure implements them.

## Files
- No direct files live in this folder; it primarily organizes child folders.

## Child Folders
- `Persistence`: Defines persistence ports consumed by the Identity application layer so handlers stay independent from EF Core details.
- `Services`: Defines service contracts that the Identity application layer relies on for time, tokens, signing keys, password hashing, and revocation.
- `Transactions`: Defines `IIdentityTransactionExecutor`, the transaction boundary for Identity write operations (it owns `SaveChangesAsync`, not the repositories).

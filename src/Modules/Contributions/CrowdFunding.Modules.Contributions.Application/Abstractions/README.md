# Abstractions

## Purpose
Contains source files related to Abstractions.

## Files
- No direct files live in this folder; it primarily organizes child folders.

## Child Folders
- `Persistence`: Defines persistence ports consumed by the Contributions application layer so handlers stay independent from EF Core details.
- `Services`: Defines service contracts that the Contributions application layer relies on for time, reads, or externalized behavior.
- `Transactions`: Defines transaction-boundary abstractions for commands in the Contributions module.

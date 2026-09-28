# Db Contexts

## Purpose
Contains EF Core database contexts for the Contributions module.

## Files
- `ContributionsDbContext.cs`: The module's DbContext: `contributions`, `active_campaigns_cache`, and (via the shared model builder extensions) `contributions_outbox_messages`, `contributions_dead_letter_events`, and `contributions_processed_events`.

## Child Folders
- None.

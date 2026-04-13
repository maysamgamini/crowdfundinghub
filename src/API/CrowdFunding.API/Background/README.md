# Background

## Purpose
Runs background infrastructure tasks such as polling module outboxes and publishing pending application events.

## Files
- `OutboxProcessorBackgroundService.cs`: Supporting source file for Outbox Processor Background Service.

## Child Folders
- None.

## Notes
The outbox processor polls campaign, contribution, and moderation databases so module events are published asynchronously after commits.

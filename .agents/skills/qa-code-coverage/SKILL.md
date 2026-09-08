---
name: qa-code-coverage
description: >-
  Use this skill to measure, observe, and audit test code coverage across all projects in the solution,
  identify untested critical paths, evaluate branch coverage, and report test coverage gaps.
---

# Code Coverage & Test Gap Audit Runbook

## Purpose
This skill guides the collection and deep analysis of automated test code coverage across all modules (`Campaigns`, `Contributions`, `Identity`, `Moderation`, `Notifications`, `CampaignUpdates`, `API`, `BuildingBlocks`).

## Coverage Collection Procedure

### 1. Execute Test Coverage Collector
Run cross-platform code coverage collection on the solution:
```bash
dotnet test CrowdFunding.slnx --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

### 2. Inspect Coverage Metrics
- Line Coverage (%)
- Branch Coverage (%)
- Method Coverage (%)
- Per-project breakdown:
  - Domain projects (target: 95%+ line coverage)
  - Application projects (target: 85%+ line coverage)
  - Infrastructure & API projects (target: 75%+ line coverage)

### 3. Critical Path Gap Identification
Check specifically for test coverage on:
- Background services (`OutboxProcessorBackgroundService`)
- Middleware & Exception filters (`GlobalExceptionHandler`, `CorrelationIdMiddleware`)
- Security & Endpoints (`JwksEndpoint`, `RateLimitingConfiguration`)
- Edge branches in command validators and event handlers
- Optimistic locking and concurrency retry loops

## Reporting
Document coverage findings, gaps, and missing test recommendations under `qa-tickets/`.

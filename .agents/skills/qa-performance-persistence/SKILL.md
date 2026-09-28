---
name: qa-performance-persistence
description: >-
  Use this skill to audit database persistence, PostgreSQL query performance, index coverage,
  EF Core query efficiency (N+1 prevention), schema migrations, and container health probes.
---

# Performance & Persistence QA Runbook

## Purpose
This skill guides the QA engineer/agent in reviewing database schemas, indexing strategies, EF Core query performance, multi-module schema migration isolation, and orchestration health checks.

## Persistence & Performance Audit Checklist

### 1. Database Indexing & Query Plans
- **Foreign Key & Filter Indexes**:
  - Audit all foreign keys in entity configurations (`CampaignId`, `ContributorId`, `UserId`, `OwnerId`). Every foreign key used in `WHERE` or `JOIN` clauses must have a supporting B-tree index.
  - Check frequently filtered columns: e.g. `contributions.campaign_id`, `contributions.status`, and the status and scheduled-time columns of each module's outbox table (e.g. `campaigns_outbox_messages`) used by the `FOR UPDATE SKIP LOCKED` claim.
  - Detect missing indexes that cause sequential table scans on high-traffic endpoints.

### 2. Multi-Module Migration Isolation
- **History Table Separation**:
  - In a modular monolith sharing a single PostgreSQL database with multiple `DbContext`s, each context must keep its own migration history. Here each module uses `MigrationsHistoryTable("__EFMigrationsHistory", "<module schema>")` (`campaigns`, `contributions`, `identity`, `moderation`, `notifications`, `campaignupdates`; the audit context uses `system`). Module tables themselves share the `public` schema, so also check for table-name collisions between modules.
- **CLI Migration Runner**:
  - Verify that database migrations can be executed independently via CLI (`dotnet run -- migrate`) rather than during application startup on production replicas.

### 3. EF Core Query Efficiency & N+1 Prevention
- Verify read services use `.AsNoTracking()` and explicit projections (`.Select(...)`) instead of loading tracked aggregate graphs for read-only queries.
- Check for N+1 queries in pagination queries (e.g. counting total rows separately or loading related collections in loops).

### 4. Container Health Checks & Probes
- Verify Kubernetes/container health endpoints:
  - `/health/live`: Shallow liveness probe (checks process responsiveness without querying downstream databases).
  - `/health/ready`: Deep readiness probe (checks PostgreSQL connectivity for all module DbContexts).

## Issue Reporting
When a performance or persistence defect is found:
1. Record it in your final report to the caller (file path and line, severity, violated rule). Do not create ticket files in the repository.
2. Document query plans, missing indexes, migration collisions, or health check deficiencies.
3. Recommend optimized indexing and persistence configurations.

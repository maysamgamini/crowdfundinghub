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
  - Check frequently filtered columns: e.g. `contributions.campaign_id`, `contributions.status`, `outbox_messages.status`, `outbox_messages.created_at_utc`.
  - Detect missing indexes that cause sequential table scans on high-traffic endpoints.

### 2. Multi-Module Migration Isolation
- **History Table Separation**:
  - In a modular monolith sharing a single PostgreSQL database with multiple `DbContext`s, each context must configure its own isolated migration history table (e.g. `x.MigrationsHistoryTable("__EFMigrationsHistory_Campaigns")`) to avoid schema version lockouts and colliding migration names.
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
1. Log a ticket in `qa-tickets/` named `TICKET-XXX-<SLUG>.md`.
2. Document query plans, missing indexes, migration collisions, or health check deficiencies.
3. Recommend optimized indexing and persistence configurations.

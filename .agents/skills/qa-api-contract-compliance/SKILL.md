---
name: qa-api-contract-compliance
description: >-
  Use this skill to audit RESTful HTTP API contracts, RFC 9457 Problem Details compliance,
  status codes, response headers, validation pipeline integration, and modular monolith architectural boundaries.
---

# API Contracts & Architectural Boundary QA Runbook

## Purpose
This skill guides the QA engineer/agent in reviewing API controllers, HTTP contract conformity, RFC 9457 Problem Details responses, validation pipelines, and modular monolith dependency constraints.

## API & Contract Audit Checklist

### 1. RFC 9457 Problem Details & Status Codes
- **Error Response Shape**:
  - Verify all error responses return `application/problem+json` with standard RFC 9457 properties (`status`, `title`, `detail`, `instance`, `traceId`).
  - Check HTTP status codes:
    - `400 Bad Request`: Client syntax errors.
    - `401 Unauthorized`: Unauthenticated caller.
    - `403 Forbidden`: Authenticated caller lacks permissions.
    - `404 Not Found`: Resource does not exist.
    - `409 Conflict`: Concurrency conflicts and unique constraint violations (ensure `ConcurrencyConflictException` and unique constraint `DbUpdateException` return 409, not 500!).
    - `422 Unprocessable Entity`: Semantic validation failures.
- **RESTful Resource Semantics**:
  - `POST` creation endpoints must return `201 Created` with a valid, accessible `Location` header pointing to the created resource (`GET /.../{id}`).
  - Check for invalid `Location` headers (e.g. `CreatedAtAction(nameof(Me))` pointing to an authenticated endpoint for unauthenticated users, or `CreatedAtAction(nameof(ListByCampaign))` instead of a single resource).
  - Verify every created resource has a corresponding `GetById` query endpoint.

### 2. Validation Pipeline Fidelity
- Check that FluentValidation validators are registered and executed for every command and query.
- Verify validation error responses map property paths cleanly into Problem Details `errors` dictionary.
- Verify that validation rules match domain aggregate constraints (e.g. min/max string length, range checks).

### 3. Modular Monolith Architecture & Boundaries
- **NetArchTest Boundary Enforcement**:
  - Verify API references only Application and Contracts, never Domain or Infrastructure directly.
  - Verify modules communicate only through Contracts, read service abstractions, and outbox application events.
  - Ensure all modules (including `Notifications` and `CampaignUpdates`) are covered in `ArchitectureTests`.

## Issue Reporting
When an API contract or boundary defect is found:
1. Log a ticket in `qa-tickets/` named `TICKET-XXX-<SLUG>.md`.
2. Document HTTP request/response payloads, headers, status codes, and violated standards.
3. Recommend modern greenfield API patterns.

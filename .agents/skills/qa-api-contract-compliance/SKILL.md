---
name: qa-api-contract-compliance
description: >-
  Use this skill to audit RESTful HTTP API contracts, RFC 9457 Problem Details compliance,
  status codes, response headers, validation pipeline integration, and modular monolith architectural boundaries.
---

# API Contracts & Architectural Boundary QA Runbook

## Purpose
This skill guides the QA engineer/agent in reviewing the HTTP API (Minimal API endpoints in `src/API/CrowdFunding.API/Endpoints/` and the one comparison controller, `Controllers/ModerationController.cs`), HTTP contract conformity, RFC 9457 Problem Details responses, validation pipelines, and modular monolith dependency constraints.

## API & Contract Audit Checklist

### 1. RFC 9457 Problem Details & Status Codes
- **Error Response Shape**:
  - Verify all error responses return `application/problem+json` with standard RFC 9457 properties (`type`, `title`, `status`, `detail`, `instance`) plus `traceId`, as written by `GlobalExceptionHandler` using `ExceptionClassifier`.
  - Check HTTP status codes:
    - `400 Bad Request`: Malformed JSON, validation failures (`RequestValidationException`, with an `errors` member) and domain rule violations (`BusinessRuleViolationException`).
    - `401 Unauthorized`: Unauthenticated caller.
    - `403 Forbidden`: Authenticated caller lacks permissions.
    - `404 Not Found`: Resource does not exist.
    - `409 Conflict`: State conflicts (`DomainConflictException`, `ResourceConflictException`), concurrency losses (`ConcurrencyConflictException`) and unique constraint violations.
    - `429 Too Many Requests`: Rate limit rejections, with `Retry-After`.
    - `503 Service Unavailable`: Database overload (connection pool exhaustion, PostgreSQL `53300`/`57P03`), with `Retry-After`.
    - `500 Internal Server Error`: Everything else, with a generic detail; framework, EF Core and Npgsql messages must never reach the client.
    - The API does not use `422`.
- **RESTful Resource Semantics**:
  - `POST` creation endpoints must return `201 Created` with a valid, accessible `Location` header pointing to the created resource (`GET /.../{id}`).
  - Check that `Location` headers come from `CreatedAtRoute` with a named GET route and resolve to the single created resource, never a hand-built string or a list route.
  - Verify every created resource has a corresponding `GetById` query endpoint.

### 2. Validation Pipeline Fidelity
- Check that a FluentValidation validator exists for every command and that `ValidationPipelineBehavior` (registered outermost) runs it before the handler; endpoints must not validate by hand. Queries are not validated by the pipeline; flag query inputs that need it.
- Verify validation error responses map property paths cleanly into Problem Details `errors` dictionary.
- Verify that validation rules match domain aggregate constraints (e.g. min/max string length, range checks).

### 3. Modular Monolith Architecture & Boundaries
- **Architecture Test Enforcement** (`tests/ArchitectureTests`, plain xUnit over compiled assembly references):
  - Verify API does not reference any module's Domain assembly (it references Infrastructure only for composition) and references no teaching sample.
  - Verify modules communicate only through Contracts, read service abstractions, and outbox application events.
  - Ensure all six modules are covered; `ModuleBoundaryConventionTests` checks every module automatically.

## Issue Reporting
When an API contract or boundary defect is found:
1. Record it in your final report to the caller (file path and line, severity, violated rule). Do not create ticket files in the repository.
2. Document HTTP request/response payloads, headers, status codes, and violated standards.
3. Recommend modern greenfield API patterns.

---
name: qa-code-cleanup
description: >-
  Use this skill to evaluate code cleanliness, code duplication, architectural smell detection,
  naming conventions, dead code, and clean refactoring opportunities in greenfield codebases.
---

# Code Cleanliness & Architecture Refactoring Runbook

## Purpose
This skill guides the evaluation and identification of areas requiring code clean-up across all layers (API, Application, Domain, Infrastructure, BuildingBlocks) in a greenfield modular monolith.

## Clean-Up Evaluation Areas

### 1. Code Duplication
- **Endpoint Utilities**: Inspect the Minimal API endpoint classes (`src/API/CrowdFunding.API/Endpoints/`) and the comparison controller for copy-pasted helpers or validation done by hand; validation belongs to `ValidationPipelineBehavior`. Move shared utilities into `BuildingBlocks`.
- **Mapping Logic**: Mapping is explicit code (no mapping library); check for the same application-result-to-response mapping duplicated across endpoints.
- **Transaction Boilerplate**: Audit transaction executor usage and repeated patterns.

### 2. Code Smells & Antipatterns
- **Magic Strings**: Find raw strings used for status values, currencies, cache keys, or policy names instead of strongly typed constants/enums.
- **Async & Cancellation Hygiene**: Ensure `CancellationToken` is passed through all async database and external calls.
- **Dummy Return Values**: Identify dummy return values (e.g. `return 0;` or `return null;`) in `ExecuteAsync` delegates that could use non-generic `ExecuteAsync(Func<CancellationToken, Task>)` overloads.
- **Unused Usings & Dead Code**: Detect unreferenced namespaces and dead branches.

### 3. Documentation & Guideline Adherence
- Verify XML documentation comments (`/// <summary>`) on all public records, classes, interfaces, and methods as mandated by `DEV_GUIDELINES.md`.
- Verify folder `README.md` completeness across new feature directories.

## Reporting
Report identified code clean-up items (file path, line, recommended change) in your final report to the caller. Do not create ticket files in the repository.

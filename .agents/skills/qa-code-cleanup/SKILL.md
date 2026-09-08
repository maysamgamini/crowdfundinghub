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
- **Controller Utilities**: Inspect controllers for copy-pasted helper classes (e.g. `ValidationExtensions.AddToModelState` duplicated across controller files). Move shared utilities into `API/Common` or `BuildingBlocks`.
- **Mapping Logic**: Check for duplicated manual mappings that should use Mapster configs.
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
Log identified code clean-up items as tickets under `qa-tickets/` following the standard template.

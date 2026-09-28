# CrowdFunding Hub Agent & Skill Catalog

This directory contains the autonomous agents, skills, and evaluation tools configured for **CrowdFunding Hub**. Each agent is specialized for a distinct domain of quality assurance, architectural rationalization, documentation, or security auditing.

---

## 1. Documentation & Architecture Agents

| Agent Name | Role | Scope & Responsibilities | Deliverables |
|---|---|---|---|
| [`doc-features-and-inline`](agents/doc-features-and-inline/agent.md) | Feature & Inline Documenter | Audits and authors exhaustive `README.md` documentation across all modules and subdirectories; enforces 100% inline XML documentation (`/// <summary>`, `<param>`, `<returns>`) across public APIs and domain models. | Complete `README.md` in every folder, XML docs in all projects, [`Directory.Build.props`](../Directory.Build.props) |
| [`openapi-platform-engineer`](agents/openapi-platform-engineer/agent.md) | OpenAPI Platform Engineer | Maintains the built-in `Microsoft.AspNetCore.OpenApi` document and Scalar UI: the JWT bearer security scheme, XML comment summaries, module tag grouping, and automated document verification tests. | [`OpenApiConfiguration.cs`](../src/API/CrowdFunding.API/Documentation/OpenApiConfiguration.cs), [`OpenApiDocumentationTests.cs`](../tests/IntegrationTests/CrowdFunding.IntegrationTests/OpenApiDocumentationTests.cs) |
| [`arch-library-architect`](agents/arch-library-architect/agent.md) | Architecture & Library Rationalizer | Evaluates architectural paradigms (Modular Monolith, Clean Architecture, CQRS dispatchers, transactional outbox, PostgreSQL concurrency primitives) and rationalizes the selection of the core libraries with alternatives considered. | [`docs/ARCHITECTURE_AND_LIBRARIES.md`](../docs/ARCHITECTURE_AND_LIBRARIES.md) |
| [`platform-shortcomings-evaluator`](agents/platform-shortcomings-evaluator/agent.md) | Platform Shortcomings Evaluator | Conducts an exhaustive audit of codebase and platform deficiencies across functional domain rules, high-load concurrency, operational containerization/CI, and security compliance. | [`docs/PLATFORM_SHORTCOMINGS.md`](../docs/PLATFORM_SHORTCOMINGS.md) |
| [`tradeoffs-limitations-analyst`](agents/tradeoffs-limitations-analyst/agent.md) | Trade-offs & Limitations Analyst | Balances core architectural trade-offs (Monolith vs Microservices, Single DB vs DB-per-service, Polling Outbox vs CDC, Advisory Locks vs Redlock, append-only ledger vs balance column), catalogs system ceilings, and outlines evolutionary triggers. | [`docs/TRADE_OFFS_AND_LIMITATIONS.md`](../docs/TRADE_OFFS_AND_LIMITATIONS.md) |

---

## 2. Quality Assurance & Engineering Agents

| Agent Name | Role | Primary Focus |
|---|---|---|
| [`qa-functional-domain`](agents/qa-functional-domain/agent.md) | Domain Verification | Aggregate invariants, state machine transitions, business boundary rules. |
| [`qa-concurrency-financial`](agents/qa-concurrency-financial/agent.md) | Concurrency & Ledgers | PostgreSQL advisory locks, `xmin` optimistic concurrency, the append-only campaign ledger, double-spend prevention. |
| [`qa-security-auth`](agents/qa-security-auth/agent.md) | Security & Pentest | Asymmetric ES256 tokens, JWKS rotation, OWASP API Top 10 compliance. |
| [`qa-resilience-outbox`](agents/qa-resilience-outbox/agent.md) | Resilience & Messaging | Transactional outbox claiming (`FOR UPDATE SKIP LOCKED`), commit-signal wake-up, lease reclaim, dead-letter handling, inbox idempotency. |
| [`qa-api-contracts`](agents/qa-api-contracts/agent.md) | API Contract Compliance | RESTful conventions, RFC 9457 Problem Details, status codes, architecture test rules. |
| [`qa-performance-persistence`](agents/qa-performance-persistence/agent.md) | Performance & Persistence | N+1 query prevention, indexing, connection pooling, cache invalidation. |
| [`qa-code-cleanup`](agents/qa-code-cleanup/agent.md) | Code Cleanliness Evaluator | Code duplication, magic strings, repository async hygiene, transaction overloads. |
| [`qa-code-coverage`](agents/qa-code-coverage/agent.md) | Code Coverage Auditor | Test line/branch coverage measurement, untested path discovery, test recommendations. |
| [`qa-e2e-api-tester`](agents/qa-e2e-api-tester/agent.md) | End-to-End API Specialist | Multi-step user journeys, route bindings, the `CrowdFundingApiFactory` integration test harness. |

---

## 3. Skills Catalog

Specialized skills mounted under `.agents/skills/` include:
- `qa-api-contract-compliance`
- `qa-code-cleanup`
- `qa-code-coverage`
- `qa-concurrency-audit`
- `qa-domain-verification`
- `qa-e2e-api-testing`
- `qa-performance-persistence`
- `qa-resilience-outbox`
- `qa-security-pentest`

QA agents and skills report their findings back to the caller; they do not write ticket files into the repository.

# CrowdFunding Hub Agent & Skill Catalog

This directory contains the autonomous agents, skills, and evaluation tools configured for **CrowdFunding Hub**. Each agent is specialized for a distinct domain of quality assurance, architectural rationalization, documentation, or security auditing.

---

## 1. Documentation & Architecture Agents

| Agent Name | Role | Scope & Responsibilities | Deliverables |
|---|---|---|---|
| [`doc-features-and-inline`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/.agents/agents/doc-features-and-inline/agent.md) | Feature & Inline Documenter | Audits and authors exhaustive `README.md` documentation across all modules and subdirectories; enforces 100% inline XML documentation (`/// <summary>`, `<param>`, `<returns>`) across public APIs and domain models. | Complete `README.md` in every folder, XML docs in all projects, [`Directory.Build.props`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/Directory.Build.props) |
| [`swagger-platform-engineer`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/.agents/agents/swagger-platform-engineer/agent.md) | Swagger Platform Engineer | Configures OpenAPI documentation, ES256 JWT Bearer security schemes, multi-assembly XML doc comment inclusion, module tag grouping, and automated Swagger verification tests. | [`SwaggerConfiguration.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/API/CrowdFunding.API/Documentation/SwaggerConfiguration.cs), [`SwaggerDocumentationTests.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/tests/IntegrationTests/CrowdFunding.IntegrationTests/SwaggerDocumentationTests.cs) |
| [`arch-library-architect`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/.agents/agents/arch-library-architect/agent.md) | Architecture & Library Rationalizer | Evaluates architectural paradigms (Modular Monolith, Clean Architecture, CQRS dispatchers, transactional outbox, PostgreSQL concurrency primitives) and rationalizes the selection of all 11 core libraries with alternatives considered. | [`docs/ARCHITECTURE_AND_LIBRARIES.md`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/docs/ARCHITECTURE_AND_LIBRARIES.md) |
| [`platform-shortcomings-evaluator`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/.agents/agents/platform-shortcomings-evaluator/agent.md) | Platform Shortcomings Evaluator | Conducts an exhaustive audit of codebase and platform deficiencies across functional domain rules, high-load concurrency, operational containerization/CI, and security compliance. | [`docs/PLATFORM_SHORTCOMINGS.md`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/docs/PLATFORM_SHORTCOMINGS.md) |
| [`tradeoffs-limitations-analyst`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/.agents/agents/tradeoffs-limitations-analyst/agent.md) | Trade-offs & Limitations Analyst | Balances core architectural trade-offs (Monolith vs Microservices, Single DB vs DB-per-service, Polling Outbox vs CDC, Advisory Locks vs Redlock), catalogs system ceilings, and outlines evolutionary triggers. | [`docs/TRADE_OFFS_AND_LIMITATIONS.md`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/docs/TRADE_OFFS_AND_LIMITATIONS.md) |

---

## 2. Quality Assurance & Engineering Agents

| Agent Name | Role | Primary Focus |
|---|---|---|
| [`qa-functional-domain`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/.agents/agents/qa-functional-domain/agent.md) | Domain Verification | Aggregate invariants, state machine transitions, business boundary rules. |
| [`qa-concurrency-financial`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/.agents/agents/qa-concurrency-financial/agent.md) | Concurrency & Ledgers | PostgreSQL advisory locks, `xmin` optimistic concurrency, double-spend prevention. |
| [`qa-security-auth`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/.agents/agents/qa-security-auth/agent.md) | Security & Pentest | Asymmetric ES256 tokens, JWKS rotation, OWASP API Top 10 compliance. |
| [`qa-resilience-outbox`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/.agents/agents/qa-resilience-outbox/agent.md) | Resilience & Messaging | Transactional outbox polling, `FOR UPDATE SKIP LOCKED`, poison message DLQ handling. |
| [`qa-api-contracts`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/.agents/agents/qa-api-contracts/agent.md) | API Contract Compliance | RESTful conventions, RFC 9457 Problem Details, status codes, NetArchTest rules. |
| [`qa-performance-persistence`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/.agents/agents/qa-performance-persistence/agent.md) | Performance & Persistence | N+1 query prevention, indexing, connection pooling, cache invalidation. |
| [`qa-code-cleanup`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/.agents/agents/qa-code-cleanup/agent.md) | Code Cleanliness Evaluator | Code duplication, magic strings, repository async hygiene, transaction overloads. |
| [`qa-code-coverage`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/.agents/agents/qa-code-coverage/agent.md) | Code Coverage Auditor | Test line/branch coverage measurement, untested path discovery, test recommendations. |
| [`qa-e2e-api-tester`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/.agents/agents/qa-e2e-api-tester/agent.md) | End-to-End API Specialist | Multi-step user journeys, route bindings, WebApplicationFactory integration test harnesses. |

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

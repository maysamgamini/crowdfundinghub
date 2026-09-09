# QA Ticket: TICKET-040

**Title:** Architectural Fitness Functions: Automated CI/CD Boundary Enforcement via GitHub Actions  
**Severity:** 🟠 P1 (High - Architectural Governance & Boundary Erosion Defense)  
**QA Focus Area:** Architectural Governance, Continuous Integration & NetArchTest Automation  
**Found By:** `qa-platform-shortcomings`  
**Status:** Open  
**Project Mode:** Greenfield (Benchmark Educational Standard)  

---

## 1. Description & Architectural Context

The solution contains a comprehensive suite of architectural guardrail tests ([`tests/ArchitectureTests/`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/tests/ArchitectureTests/CrowdFunding.ArchitectureTests/README.md)) that enforce Clean Architecture layers and module boundaries using NetArchTest.

However, the repository currently has **zero automated CI/CD pipelines**:
- No `.github/workflows/` directory.
- No automated pull-request validation or branch protection enforcement.

### The Architectural Boundary Erosion Hazard
In real-world engineering teams:
1. Developers work under aggressive deadlines.
2. If tests are not executed automatically on every commit and pull request, developers will inadvertently introduce forbidden direct references (e.g. referencing another module's `Domain` or `Infrastructure` assembly).
3. Without CI automation, **architectural tests are passive code sitting on disk** that does not protect the codebase from eroding into a tangled monolith.

---

## 2. Blast Radius & Governance Impact

- **Silent Boundary Erosion:** Architectural violations bypass review and merge into default branches, destroying the modular monolith's microservice-readiness over time.
- **Broken Builds in Master:** Code compiling locally on one developer's machine may break in clean container builds due to uncommitted dependencies or environment drift.

---

## 3. Educational Rationale: Teaching Principals & Architects

### The Pedagogical Objective
Teach the practice of **Architectural Fitness Functions (Building Evolutionary Architectures)**. An architect's job is not just to define clean boundaries, but to **automate their enforcement** so that the architecture defends itself against human error and organizational entropy.

### Monolith First, Microservices Ready
The outcome of this project is a **Modular Monolith, NOT microservices**. But maintaining the modular discipline of a monolith requires relentless automated governance. By codifying:
1. Compilation with zero warnings,
2. Unit tests,
3. NetArchTest architectural boundary tests, and
4. Testcontainers integration test verification
into an automated GitHub Actions CI workflow (`.github/workflows/ci.yml`), the Monolith **guarantees that its microservice-ready boundaries remain permanently pristine** through every pull request.

### What Breaks Tomorrow If Ignored Today?
If an architect does not enforce boundary rules in CI, within 6 months the modular monolith becomes heavily tangled with circular dependencies and cross-module SQL queries, making future microservice extraction impossible without a complete rewrite.

---

## 4. Affected Files & Modules

- Creation of `.github/workflows/ci.yml`
- Root solution file: `CrowdFunding.slnx`
- All test projects in `tests/`

---

## 5. Implementation Specification & Greenfield Solution

```mermaid
graph TD
    PR[Developer Opens Pull Request] --> CI[GitHub Actions Runner]
    
    subgraph "Automated Architectural Fitness Pipeline"
        CI --> Step1["1. Setup .NET 10 SDK & Docker"]
        Step1 --> Step2["2. dotnet restore & dotnet format --verify"]
        Step2 --> Step3["3. dotnet build -c Release (TreatWarningsAsErrors)"]
        Step3 --> Step4["4. Unit Tests (Domain Rules)"]
        Step4 --> Step5["5. Architecture Tests (NetArchTest Boundary Enforcement)"]
        Step5 --> Step6["6. Integration Tests (PostgreSQL Testcontainers)"]
    end

    Step6 -->|All Passed| Merge["✅ PR Approved & Mergeable"]
    Step5 -->|Boundary Violated| Block["❌ PR Blocked: Forbidden Reference Detected!"]
```

### Step 1: Author `.github/workflows/ci.yml`
```yaml
name: Continuous Integration & Architectural Governance

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main, develop ]

jobs:
  build-and-verify:
    name: Build, Test & Architecture Verification
    runs-on: ubuntu-latest

    steps:
      - name: Checkout Code
        uses: actions/checkout@v4

      - name: Setup .NET 10 SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore Dependencies
        run: dotnet restore CrowdFunding.slnx

      - name: Verify Code Formatting
        run: dotnet format CrowdFunding.slnx --verify-no-changes --verbosity diagnostic

      - name: Build Solution (Zero Warnings Tolerance)
        run: dotnet build CrowdFunding.slnx --configuration Release --no-restore /warnaserror

      - name: Execute Architectural Guardrails (NetArchTest)
        run: dotnet test tests/ArchitectureTests/CrowdFunding.ArchitectureTests/CrowdFunding.ArchitectureTests.csproj --configuration Release --no-build --logger "trx;LogFileName=arch-results.trx"

      - name: Execute Unit Tests
        run: dotnet test tests/UnitTests/CrowdFunding.UnitTests/CrowdFunding.UnitTests.csproj --configuration Release --no-build --logger "trx;LogFileName=unit-results.trx"

      - name: Execute Integration Tests (Testcontainers PostgreSQL)
        run: dotnet test tests/IntegrationTests/CrowdFunding.IntegrationTests/CrowdFunding.IntegrationTests.csproj --configuration Release --no-build --logger "trx;LogFileName=integration-results.trx"
```

---

## 6. Verification & Acceptance Criteria

1. **Pipeline Execution:** Pushing a commit or opening a PR triggers GitHub Actions executing all 171 tests and formatting validations.
2. **Boundary Protection Test:** Intentionally adding a forbidden reference (e.g. `Identity.Domain` directly referenced in `CrowdFunding.API`) causes the CI step `Execute Architectural Guardrails` to fail and block the merge.
3. **Testcontainers in Linux Runner:** Integration tests successfully launch ephemeral PostgreSQL containers inside the Ubuntu runner without configuration errors.

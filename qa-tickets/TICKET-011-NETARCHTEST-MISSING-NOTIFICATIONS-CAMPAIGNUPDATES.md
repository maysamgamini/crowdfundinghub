# QA Ticket: TICKET-011

**Title:** Architecture Tests Omit `Notifications` and `CampaignUpdates` Modules, Leaving Boundary Enforcements Incomplete  
**Severity:** 🟡 P2 (Medium - Test Suite Guardrail Defect)  
**QA Focus Area:** Architecture Tests & Modular Boundary QA  
**Found By:** `qa-api-contracts`  
**Status:** Resolved (Fixed via project references added to CrowdFunding.ArchitectureTests.csproj)  
**Project Mode:** Greenfield (No backward compatibility required)  

---

## 1. Description
In `tests/ArchitectureTests/CrowdFunding.ArchitectureTests/`, there are architecture dependency test suites for only 4 of the 6 modules:
- `CampaignsModuleDependencyTests.cs`
- `ContributionsModuleDependencyTests.cs`
- `IdentityModuleDependencyTests.cs`
- `ModerationModuleDependencyTests.cs`

The following two modules are completely omitted from the architecture test suite:
- `CrowdFunding.Modules.Notifications` (`Application`, `Domain`, `Infrastructure`)
- `CrowdFunding.Modules.CampaignUpdates` (`Application`, `Domain`, `Infrastructure`)

## 2. Blast Radius & Defect Reproduction
1. If a developer accidentally adds a direct reference from `Notifications` or `CampaignUpdates` to another module's internal `Domain` or `Infrastructure` project (or vice versa), `dotnet test` will pass without warning.
2. The modular monolith's strict isolation boundaries will slowly degrade undetected in CI/CD.

## 3. Affected Files
- [`tests/ArchitectureTests/CrowdFunding.ArchitectureTests/`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/tests/ArchitectureTests/CrowdFunding.ArchitectureTests/)

## 4. Recommended Fix (Greenfield)
Add NetArchTest test classes for the two missing modules:
1. `NotificationsModuleDependencyTests.cs`: Enforce that `Notifications.Application` depends only on `BuildingBlocks` and contract assemblies, never on other modules' domains or persistence.
2. `CampaignUpdatesModuleDependencyTests.cs`: Enforce that `CampaignUpdates.Application` depends only on `BuildingBlocks` and contract assemblies.

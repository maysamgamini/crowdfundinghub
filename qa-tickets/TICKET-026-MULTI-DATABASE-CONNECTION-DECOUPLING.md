# QA Ticket: TICKET-026

**Title:** Multi-Database Connection Decoupling: Enable Database-Per-Service Topology via Fallback Connection Strings  
**Severity:** 🟡 P2 (Medium - Architectural Flexibility & Deployment Topology)  
**QA Focus Area:** Database Isolation, Microservices Deployment & Configuration Architecture  
**Found By:** `qa-architect-curriculum`  
**Status:** Open  
**Project Mode:** Greenfield (Benchmark Educational Standard)  

---

## 1. Description & Architectural Context

Every module infrastructure DI extension currently hardcodes a single, shared connection string key: `"DefaultConnection"`.

Example from [`CampaignsInfrastructureDependencyInjection.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Infrastructure/DependencyInjection/CampaignsInfrastructureDependencyInjection.cs#L27-L28):
```csharp
var connectionString = configuration.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");
```
The same hardcoded lookup appears in `ContributionsInfrastructureDependencyInjection.cs`, `IdentityInfrastructureDependencyInjection.cs`, and `ModerationInfrastructureDependencyInjection.cs`.

### Why This Impedes Microservice Decomposition
One of the primary goals taught to Principal Engineers and Architects is the evolutionary database migration pattern:
1. **Stage 1 (Monolith):** Single Database Instance $\to$ Multiple PostgreSQL Schemas (`campaigns`, `contributions`, etc.).
2. **Stage 2 (Decomposition Transition):** Single Database Host $\to$ Multiple Logical Databases (`campaigns_db`, `contributions_db`, etc.).
3. **Stage 3 (Full Microservices):** Independent Physical Database Clusters (e.g. AWS Aurora for Contributions, Azure Cosmos/Postgres for Identity).

Because all modules strictly require `"DefaultConnection"`, it is impossible to configure distinct connection strings per module in Docker Compose, Kubernetes ConfigMaps, or production environments without modifying C# code.

---

## 2. Blast Radius & Decomposition Impact

- **Deployment Rigidity:** Cannot run modules against isolated physical or logical database instances during integration testing or staging environments.
- **Connection Pool Exhaustion:** All modules drain connections from the exact same pool. A query leak or surge in `Identity` or `Campaigns` exhausts connections needed by financial transaction processing in `Contributions`.

---

## 3. Educational Rationale: Teaching Principals & Architects

### The Pedagogical Objective
Teach the evolutionary pattern of **Progressive Data Layer Decoupling**. Architects should understand that you do not jump from a single shared database directly into multi-region microservice database clusters. There is an evolutionary path: Single DB with Shared Schemas $\to$ Single DB with Isolated Schemas $\to$ Multiple Logical Databases $\to$ Physically Isolated Database Clusters.

### Monolith First, Microservices Ready
The outcome of this project is a **single, unified Modular Monolith running against a single PostgreSQL database instance** (`DefaultConnection`). Developers run one Docker container or local PostgreSQL instance and the entire application works seamlessly. However, by designing the infrastructure layer with fallback hierarchical resolution (`CampaignsDb ?? DefaultConnection`), the Monolith is **100% physically decoupled at the connection boundary**. When turning any module into a microservice, SREs can provision an isolated AWS RDS instance for that service and supply its connection string via environment variables with zero C# code changes or re-compilation.

### What Breaks Tomorrow If Ignored Today?
If a monolith hardcodes a single `"DefaultConnection"` across all modules, you cannot perform blue/green database migrations for a single module, you cannot isolate connection pool starvation, and extracting a microservice requires painful code modifications to DbContext registrations across multiple assemblies.

---

## 4. Affected Files & Modules

- [`src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Infrastructure/DependencyInjection/CampaignsInfrastructureDependencyInjection.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Infrastructure/DependencyInjection/CampaignsInfrastructureDependencyInjection.cs#L27)
- [`src/Modules/Contributions/CrowdFunding.Modules.Contributions.Infrastructure/DependencyInjection/ContributionsInfrastructureDependencyInjection.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Infrastructure/DependencyInjection/ContributionsInfrastructureDependencyInjection.cs)
- [`src/Modules/Identity/CrowdFunding.Modules.Identity.Infrastructure/DependencyInjection/IdentityInfrastructureDependencyInjection.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Identity/CrowdFunding.Modules.Identity.Infrastructure/DependencyInjection/IdentityInfrastructureDependencyInjection.cs)
- [`src/Modules/Moderation/CrowdFunding.Modules.Moderation.Infrastructure/DependencyInjection/ModerationInfrastructureDependencyInjection.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Moderation/CrowdFunding.Modules.Moderation.Infrastructure/DependencyInjection/ModerationInfrastructureDependencyInjection.cs)

---

## 4. Implementation Specification & Greenfield Solution

Implement hierarchical fallback connection string resolution across all modules.

### Resolution Hierarchy:
1. **Module-Specific Connection String** (e.g., `ConnectionStrings:CampaignsDb` or `ConnectionStrings:ContributionsDb`).
2. **Fallback Shared Connection String** (`ConnectionStrings:DefaultConnection`).
3. **Descriptive Exception** detailing the missing keys if neither is present.

### Implementation Pattern:
Define an extension method in `CrowdFunding.BuildingBlocks.Infrastructure`:
```csharp
namespace CrowdFunding.BuildingBlocks.Infrastructure.Configuration;

public static class ConfigurationExtensions
{
    public static string GetRequiredModuleConnectionString(
        this IConfiguration configuration,
        string moduleKey,
        string fallbackKey = "DefaultConnection")
    {
        var connectionString = configuration.GetConnectionString(moduleKey)
                               ?? configuration.GetConnectionString(fallbackKey);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Neither module-specific connection string '{moduleKey}' nor fallback '{fallbackKey}' was configured.");
        }

        return connectionString;
    }
}
```

### Module Registration Usage:
```csharp
// In CampaignsInfrastructureDependencyInjection.cs
var connectionString = configuration.GetRequiredModuleConnectionString("CampaignsDb");

// In ContributionsInfrastructureDependencyInjection.cs
var connectionString = configuration.GetRequiredModuleConnectionString("ContributionsDb");

// In ModerationInfrastructureDependencyInjection.cs
var connectionString = configuration.GetRequiredModuleConnectionString("ModerationDb");
```

### Configuration Example (`appsettings.Production.json` or Environment Variables):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=shared-postgres;Database=crowdfunding;Username=app;Password=secret",
    "ContributionsDb": "Host=financial-cluster-aurora;Database=contributions;Username=fin_app;Password=secret"
  }
}
```

---

## 5. Verification & Acceptance Criteria

1. **Monolithic Backwards Compatibility:** With only `ConnectionStrings:DefaultConnection` configured, all 4 modules start, migrate, and operate normally.
2. **Independent Database Isolation Test:** Add an integration test configuring `CampaignsDb` pointing to a secondary Postgres container and verify `CampaignsDbContext` communicates exclusively with the secondary database while `ContributionsDbContext` uses the primary.
3. **Informative Error Messages:** If neither connection string is present, the thrown exception clearly states both expected keys.

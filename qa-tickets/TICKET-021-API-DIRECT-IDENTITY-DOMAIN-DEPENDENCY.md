# QA Ticket: TICKET-021

**Title:** Architecture Boundary Violation: `AdminSeeder` in API Layer Directly References `Identity.Domain`, Failing Architecture Tests  
**Severity:** 🔴 P1 (High - Architecture Boundary Violation & Test Failure)  
**QA Focus Area:** Clean Architecture & Modular Monolith Isolation  
**Found By:** `qa-code-cleanliness`  
**Status:** Open  
**Project Mode:** Greenfield (No backward compatibility required)  

---

## 1. Description
Executing `dotnet test` currently fails on the architecture test suite:
```
[xUnit.net] CrowdFunding.ArchitectureTests.IdentityModuleDependencyTests.Api_ShouldNotReference_IdentityDomainDirectly [FAIL]
  Assert.DoesNotContain() Failure: Item found in collection
Collection: [···, "CrowdFunding.Modules.Identity.Domain", ···]
Found:      "CrowdFunding.Modules.Identity.Domain"
```

The source of this direct dependency is [`src/API/CrowdFunding.API/Migrations/AdminSeeder.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/API/CrowdFunding.API/Migrations/AdminSeeder.cs#L4):
```csharp
using CrowdFunding.Modules.Identity.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Identity.Application.Abstractions.Services;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using CrowdFunding.Modules.Identity.Domain.Aggregates; // <-- Direct domain reference from API

namespace CrowdFunding.API.Migrations;

public static class AdminSeeder
{
    public static async Task RunAsync(IServiceProvider services, string email, string password, string displayName, CancellationToken cancellationToken = default)
    {
        // ...
        var normalizedEmail = User.NormalizeEmailAddress(email);
        // ...
        var user = User.Register(email, displayName, passwordHasher.HashPassword(password), dateTimeProvider.UtcNow);
        user.AssignRole(RoleConstants.Admin);
        await userRepository.AddAsync(user, cancellationToken);
    }
}
```

This violates the explicit architecture boundary rule documented in [`DEV_GUIDELINES.md`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/DEV_GUIDELINES.md#L29):
> *"Avoid direct API-to-domain references. The architecture tests already guard this pattern for the main modules."*

Because .NET project references transitively propagate assembly references, `AdminSeeder` directly imports the domain model `User` into the API assembly, breaking the architectural test guardrail.

## 2. Blast Radius & Defect Reproduction
1. `dotnet test` currently fails in CI/CD pipeline runs.
2. The core modular monolith boundary (API $\to$ Contracts/Application $\to$ Domain) is breached, allowing developers to bypass application orchestration and instantiate domain aggregates directly in presentation-layer controllers and CLI seeders.

## 3. Affected Files
- [`src/API/CrowdFunding.API/Migrations/AdminSeeder.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/API/CrowdFunding.API/Migrations/AdminSeeder.cs#L4)
- [`tests/ArchitectureTests/CrowdFunding.ArchitectureTests/IdentityModuleDependencyTests.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/tests/ArchitectureTests/CrowdFunding.ArchitectureTests/IdentityModuleDependencyTests.cs#L30-L35)

## 4. Recommended Fix (Greenfield)

### Step 1: Encapsulate Admin Seeding in `Identity.Application`
Define an abstraction or command in `CrowdFunding.Modules.Identity.Application`:
```csharp
namespace CrowdFunding.Modules.Identity.Application.Abstractions.Services;

public interface IAdminUserSeeder
{
    Task SeedAdminUserAsync(string email, string password, string displayName, CancellationToken cancellationToken = default);
}
```
Implement `AdminUserSeeder` inside `Identity.Application` (or `Identity.Infrastructure`) where `User` aggregate access is architecturally valid.

### Step 2: Refactor `AdminSeeder.cs` in API
In `CrowdFunding.API/Migrations/AdminSeeder.cs`, remove `using CrowdFunding.Modules.Identity.Domain.Aggregates;` and delegate directly to `IAdminUserSeeder`:
```csharp
public static class AdminSeeder
{
    public static async Task RunAsync(IServiceProvider services, string email, string password, string displayName, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IAdminUserSeeder>();
        await seeder.SeedAdminUserAsync(email, password, displayName, cancellationToken);
    }
}
```

### Step 3: Verify Architecture Test
Run `dotnet test --filter IdentityModuleDependencyTests` to confirm `Api_ShouldNotReference_IdentityDomainDirectly` passes.

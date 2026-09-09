# QA Ticket: TICKET-012

**Title:** Missing `WebApplicationFactory<Program>` Integration Test Harness and Inaccessible `Program` Class  
**Severity:** 🔴 P1 (High - Test Infrastructure & Coverage Gap)  
**QA Focus Area:** Integration Testing & HTTP Test Harness  
**Found By:** `qa-api-contracts` / `qa-integration-testing`  
**Status:** Fixed  
**Project Mode:** Greenfield (No backward compatibility required)  

---

## 1. Description
A full audit of `tests/IntegrationTests` reveals that there is currently **zero** automated testing of the HTTP API pipeline across the CrowdFunding platform:

1. **Inaccessible Entry Point:** In `src/API/CrowdFunding.API/Program.cs`, top-level statements are used without a `public partial class Program { }` declaration at the bottom. As a result, the generated `Program` class is internal and cannot be referenced by external test projects using ASP.NET Core's `WebApplicationFactory<Program>`.
2. **Missing Test Sdk Dependencies:** `tests/IntegrationTests/CrowdFunding.IntegrationTests/CrowdFunding.IntegrationTests.csproj` does not include a reference to `Microsoft.AspNetCore.Mvc.Testing`.
3. **Absence of HTTP Pipeline Integration Tests:** The only existing integration tests (`CampaignContributionConcurrencyTests` and `ContributionConcurrencyTests`) directly instantiate Entity Framework DbContexts, repositories, and command handlers against a PostgreSQL Testcontainer. They completely bypass:
   - ASP.NET Core routing and endpoint dispatching
   - Controller model binding, FluentValidation error handling, and model state mapping
   - JWT Bearer authentication, claims extraction, and authorization policy enforcement
   - ASP.NET Core RateLimiting middleware (`auth`, `payment`)
   - RFC 9457 `GlobalExceptionHandler` error transformations and problem details formatting
   - Content negotiation, HTTP status codes, and `Location` headers
   - Asynchronous outbox polling and SignalR `CampaignHub` WebSocket broadcasts
4. **No Synchronous Outbox Test Hook:** Because outbox messages are processed asynchronously by `OutboxProcessorBackgroundService` with a 5-second polling interval, integration tests attempting to exercise cross-module workflows (e.g. Campaign Created -> Moderation Review Created, or Contribution Confirmed -> Campaign Raised Amount Credited) will suffer from non-deterministic race conditions without an explicit harness method to trigger outbox message processing on demand.

## 2. Blast Radius & Defect Reproduction
1. Developers modifying controller routes, authorization policies, validation rules, or Mapster configurations cannot detect regressions locally or in CI through automated tests.
2. Production regressions in critical user journeys (e.g. broken JWT claims deserialization, rate limiting rejecting valid requests, broken Location header generation, or unhandled 500 errors) escape into staging and production undetected.
3. High-concurrency financial operations have verified database semantics, but the HTTP API endpoints that trigger them remain completely untested.

## 3. Affected Files
- [`src/API/CrowdFunding.API/Program.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/API/CrowdFunding.API/Program.cs)
- [`tests/IntegrationTests/CrowdFunding.IntegrationTests/CrowdFunding.IntegrationTests.csproj`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/tests/IntegrationTests/CrowdFunding.IntegrationTests/CrowdFunding.IntegrationTests.csproj)
- `tests/IntegrationTests/CrowdFunding.IntegrationTests/` (test harness missing)

## 4. Recommended Fix (Greenfield)

### Step 1: Expose `Program` in `src/API/CrowdFunding.API/Program.cs`
Append to the end of `Program.cs`:
```csharp
public partial class Program;
```

### Step 2: Add Package Reference to `CrowdFunding.IntegrationTests.csproj`
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.5" />
</ItemGroup>
```

### Step 3: Implement `CrowdFundingApiFactory` Fixture
Create a modern, reusable `WebApplicationFactory<Program>` test fixture backed by PostgreSQL Testcontainers that:
1. Applies migrations for all 4 module databases (`CampaignsDbContext`, `ContributionsDbContext`, `IdentityDbContext`, `ModerationDbContext`).
2. Configures a test authentication scheme or helper to issue valid JWTs with arbitrary claims/permissions.
3. Exposes an `ExecuteOutboxBatchAsync()` helper so integration tests can deterministically advance the outbox state machine without arbitrary `Task.Delay` sleeps:

```csharp
public sealed class CrowdFundingApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("crowdfunding_e2e_tests")
        .Build();

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
        using var scope = Services.CreateScope();
        await MigrationRunner.RunAsync(scope.ServiceProvider);
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _dbContainer.GetConnectionString(),
                ["Database:ConnectionString"] = _dbContainer.GetConnectionString(),
                ["Jwt:Issuer"] = "CrowdFunding.Test",
                ["Jwt:Audience"] = "CrowdFunding.Test"
            });
        });
    }

    public async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken = default)
    {
        using var scope = Services.CreateScope();
        var backgroundService = scope.ServiceProvider
            .GetServices<IHostedService>()
            .OfType<OutboxProcessorBackgroundService>()
            .FirstOrDefault();

        // Or directly invoke ClaimPendingBatchAsync + PublishAsync on each DbContext
    }
}
```

### Step 4: Implement End-to-End Multi-Step Journey Tests
Create automated HTTP test suites under `tests/IntegrationTests`:
- `UserAuthenticationE2ETests.cs`: Registration -> Login -> Get `/me` -> Role Assignment.
- `CampaignLifecycleE2ETests.cs`: Create Campaign -> Process Outbox -> Moderation Approve -> Publish Campaign.
- `ContributionAndOutboxE2ETests.cs`: Make Contribution -> Confirm Payment -> Process Outbox -> Verify Campaign Raised Amount and SignalR pledge broadcast.

---

## Resolution Note (doc reconciliation pass)

This ticket's fix already landed in commit `bdfb810` earlier in this session's branch history; the `Status` field above was not updated at the time. Verified against current code during the TICKET-036/037/039 follow-up audit (2026-09-08) — the described defect no longer reproduces.

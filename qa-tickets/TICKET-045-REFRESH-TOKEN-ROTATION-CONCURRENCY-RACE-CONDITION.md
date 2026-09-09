# QA Ticket: TICKET-045

**Title:** Refresh Token Rotation Concurrency Hazard: Missing Optimistic Concurrency Token (`xmin`) Enables Parallel Token Desync  
**Severity:** 🟠 P1 (High - Authentication Session Security & False Breach Panics)  
**QA Focus Area:** Concurrency, Decentralized Security & Session Lifecycle  
**Found By:** `qa-security-pentest` & `qa-concurrency-audit`  
**Status:** Open  
**Project Mode:** Greenfield (Benchmark Educational Standard)  

---

## 1. Description & Architectural Context

In TICKET-036, Refresh Token Rotation (RTR) was introduced via [`RefreshToken`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Identity/CrowdFunding.Modules.Identity.Domain/Entities/RefreshToken.cs) and [`RefreshAccessTokenCommandHandler`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Identity/CrowdFunding.Modules.Identity.Application/Features/Users/Commands/RefreshAccessToken/RefreshAccessTokenCommandHandler.cs).

When a refresh token is presented:
1. The handler loads `storedToken = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, ...)`.
2. It checks `if (storedToken.IsRevoked) { await HandleReuseAsync(...); throw ... }`.
3. If active, it revokes `storedToken` by setting `IsRevoked = true`, records `ReplacedByTokenHash = newTokenHash`, and persists a new `RefreshToken` row.

### The Race Condition Flaw
Inspecting [`RefreshTokenConfiguration.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Identity/CrowdFunding.Modules.Identity.Infrastructure/Persistence/Configurations/RefreshTokenConfiguration.cs):
```csharp
builder.ToTable("refresh_tokens");
builder.HasKey(x => x.Id);
builder.Property(x => x.TokenHash).HasColumnName("token_hash").IsRequired();
builder.HasIndex(x => x.TokenHash).IsUnique();
// NOTICE: NO xmin optimistic concurrency token configured!
```

Neither `RefreshToken` nor `RefreshAccessTokenCommandHandler` employs any concurrency control (no PostgreSQL `xmin` row version token, and no advisory locking).

---

## 2. Blast Radius & False Breach Panic Attack Scenario

Consider a client application that triggers two parallel API calls upon access token expiry (e.g. fetching user profile and fetching notification count simultaneously). Both HTTP interceptors attempt to rotate the refresh token at the exact same millisecond:

1. **Request A & Request B** both execute `GetByTokenHashAsync(tokenHash)` concurrently.
2. Both read the same unrevoked row (`IsRevoked == false`).
3. **Request A** enters `ExecuteAsync`, calls `storedToken.Revoke(now, newTokenA)`, adds `newRefreshTokenA`, and commits.
4. **Request B** enters `ExecuteAsync`, calls `storedToken.Revoke(now, newTokenB)`, adds `newRefreshTokenB`, and commits!
5. **The Audit Chain Is Clobbered:** In the database, `storedToken.ReplacedByTokenHash` is overwritten with `newTokenB`.
6. When Request A stores `newTokenA` on the mobile device and uses it on the next refresh:
   - The server inspects `storedToken.ReplacedByTokenHash` and sees `newTokenB != newTokenA`!
   - The security mechanism assumes `newTokenA` is an intercepted, replayed token from an attacker!
   - **False Breach Panic:** It invokes `HandleReuseAsync`, revoking ALL sessions and logging the legitimate user out of all devices globally!

---

## 3. Educational Rationale: Teaching Principals & Architects

### The Pedagogical Objective
Teach architects why **State Transitions in Security Credentials Require Optimistic Concurrency Control**. A cryptographic token rotation is not a simple append-only log; it is a single-use transition. If multiple concurrent requests present the same credential, exactly one must succeed, and the losing request must encounter an immediate concurrency conflict rather than overwriting the rotation chain.

### Monolith First, Microservices Ready
In distributed OAuth2 / OIDC authorization servers, token rotation endpoints face extreme concurrency from mobile apps, web workers, and multi-tab browser sessions. Implementing optimistic locking via PostgreSQL `xmin` on refresh token entities provides deterministic single-winner rotation semantics.

---

## 4. Affected Files & Modules

- [`src/Modules/Identity/CrowdFunding.Modules.Identity.Domain/Entities/RefreshToken.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Identity/CrowdFunding.Modules.Identity.Domain/Entities/RefreshToken.cs)
- [`src/Modules/Identity/CrowdFunding.Modules.Identity.Infrastructure/Persistence/Configurations/RefreshTokenConfiguration.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Identity/CrowdFunding.Modules.Identity.Infrastructure/Persistence/Configurations/RefreshTokenConfiguration.cs)
- [`src/Modules/Identity/CrowdFunding.Modules.Identity.Application/Features/Users/Commands/RefreshAccessToken/RefreshAccessTokenCommandHandler.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Identity/CrowdFunding.Modules.Identity.Application/Features/Users/Commands/RefreshAccessToken/RefreshAccessTokenCommandHandler.cs)

---

## 5. Greenfield Remediation Guidance

1. Configure `builder.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();` in `RefreshTokenConfiguration.cs` (mirroring `UserConfiguration` and `CampaignConfiguration`).
2. Alternatively, derive `AdvisoryLockKey.FromGuid(storedToken.Id)` or execute the update with an atomic SQL check: `UPDATE refresh_tokens SET is_revoked = true, replaced_by_token_hash = @newHash WHERE id = @id AND is_revoked = false`.
3. If a concurrency conflict occurs on the losing parallel request, return a clean `409 Conflict` or retry gracefully so that mobile clients do not experience random global session revocations.

---

## 6. Verification & Acceptance Criteria

1. **Concurrency Protection Configured:** `RefreshTokenConfiguration` defines `xmin` as a concurrency token.
2. **Parallel Rotation Stress Test:** 10 concurrent requests presenting the same refresh token result in exactly ONE successful rotation issuing a new token, while 9 requests are rejected with concurrency conflicts without revoking the entire user session family.

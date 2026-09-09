# QA Ticket: TICKET-049

**Title:** Administrative Security Defect: Missing User Deactivation API & Administrative Session Revocation Controls  
**Severity:** 🟠 P1 (High - Security Administration & Incident Response)  
**QA Focus Area:** Application Security, Administrative Authorization & Session Invalidation  
**Found By:** `qa-security-pentest`  
**Status:** Fixed  
**Project Mode:** Greenfield (Benchmark Educational Standard)  

---

## 1. Description & Architectural Context

In [`User.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Identity/CrowdFunding.Modules.Identity.Domain/Aggregates/User.cs#L98-L106), the domain aggregate defines the core account deactivation method:
```csharp
public void Deactivate()
{
    IsActive = false;
    RotateSecurityStamp();
}
```

The domain logic is intentionally paired with security stamp rotation so that deactivating a compromised or fraudulent user immediately triggers decentralized session revocation via Redis blacklist (TICKET-036).

### The Administrative API Gap
However, an audit of the API controllers and Application features reveals:
1. There is **NO `POST /api/identity/users/{id}/deactivate` HTTP endpoint**.
2. There is **NO `DeactivateUserCommand` or `DeactivateUserCommandHandler`**.
3. While the database schema supports `is_active = false`, there is no operational mechanism for an Administrator holding `users:manage` or `admin` roles to suspend an abusive creator, ban a fraudster, or terminate access for a compromised account.
4. If an account is breached or used for money laundering, the only way to deactivate the account is for an engineer to manually connect to production PostgreSQL and execute raw SQL statements!

---

## 2. Blast Radius & Security Impact

- **Incident Response Paralysis:** During an active credential stuffing attack, fraud spree, or account takeover, security administrators have no API or administrative tool to revoke user sessions or deactivate the account.
- **SOC 2 & ISO 27001 Non-Compliance:** Fails Security Incident Management and Access Control requirements mandating the ability to immediately terminate user access via standardized administrative channels.

---

## 3. Educational Rationale: Teaching Principals & Architects

### The Pedagogical Objective
Teach architects why **Domain Methods Must Be Exposed Through Explicit Administrative CQRS Command Pipelines**. Having logic in a domain model is meaningless if administrative authorization, audit logging, and transactional outbox hooks cannot orchestrate it through a secure, permission-gated endpoint.

### Monolith First, Microservices Ready
Administrative boundaries require strict role-based policy enforcement (`[Authorize(Policy = PermissionConstants.UsersManage)]`). Exposing this command ensures that when identity management is administered through internal back-office portals, full non-repudiation and audit logging are enforced.

---

## 4. Affected Files & Modules

- [`src/Modules/Identity/CrowdFunding.Modules.Identity.Domain/Aggregates/User.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Identity/CrowdFunding.Modules.Identity.Domain/Aggregates/User.cs)
- [`src/Modules/Identity/CrowdFunding.Modules.Identity.Application/`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Identity/CrowdFunding.Modules.Identity.Application/)
- [`src/API/CrowdFunding.API/Controllers/IdentityController.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/API/CrowdFunding.API/Controllers/IdentityController.cs)

---

## 5. Greenfield Remediation Guidance

1. Create `DeactivateUserCommand(Guid UserId)` and `DeactivateUserCommandValidator`.
2. Implement `DeactivateUserCommandHandler`:
   - Enforce caller has administrative privileges (`UsersManage`).
   - Load user inside transaction, record previous security stamp.
   - Call `user.Deactivate()`.
   - Publish old security stamp to `ISecurityStampRevocationStore` (with 15-minute TTL).
   - Revoke all active refresh tokens for the user in `IRefreshTokenRepository`.
3. In `IdentityController.cs`, expose:
   ```csharp
   [Authorize(Policy = PermissionConstants.UsersManage)]
   [HttpPost("users/{id:guid}/deactivate")]
   [ProducesResponseType(StatusCodes.Status200OK)]
   [ProducesResponseType(StatusCodes.Status403Forbidden)]
   [ProducesResponseType(StatusCodes.Status404NotFound)]
   public async Task<IActionResult> Deactivate([FromRoute] Guid id, CancellationToken cancellationToken)
   ```

---

## 6. Verification & Acceptance Criteria

1. **Endpoint Authorization:** Calling `POST /api/identity/users/{id}/deactivate` without admin permissions returns `403 Forbidden`.
2. **Instant Invalidation:** Deactivating a user immediately causes:
   - Subsequent `GET /api/identity/me` with existing access token to return `401 Unauthorized` (via security stamp blacklist).
   - Subsequent `POST /api/identity/refresh` with existing refresh token to return `401 Unauthorized`.
   - Subsequent `POST /api/identity/login` to return `401 Unauthorized`.

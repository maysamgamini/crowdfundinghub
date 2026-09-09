# QA Ticket: TICKET-010

**Title:** `IdentityController.Register` Sets `Location` Header to `/api/Identity/me`, which Returns 401 Unauthorized for Unauthenticated Callers  
**Severity:** 🟡 P2 (Medium - API Contract & Client Navigation)  
**QA Focus Area:** API Contracts & Auth QA  
**Found By:** `qa-api-contracts` / `qa-security-auth`  
**Status:** Fixed  
**Project Mode:** Greenfield (No backward compatibility required)  

---

## 1. Description
In `IdentityController.cs`:

```csharp
[AllowAnonymous]
[EnableRateLimiting(RateLimitingConfiguration.AuthPolicy)]
[HttpPost("register")]
[ProducesResponseType(typeof(RegisterUserResponse), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
public async Task<ActionResult<RegisterUserResponse>> Register(
    [FromBody] RegisterUserRequest request,
    CancellationToken cancellationToken)
{
    var command = _mapper.Map<RegisterUserCommand>(request);
    var validationResult = await _registerValidator.ValidateAsync(command, cancellationToken);
    ...
    var result = await _commandDispatcher.SendAsync<RegisterUserResult>(command, cancellationToken);
    return CreatedAtAction(nameof(Me), _mapper.Map<RegisterUserResponse>(result));
}

[Authorize]
[HttpGet("me")]
[ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
public async Task<ActionResult<CurrentUserResponse>> Me(CancellationToken cancellationToken)
{
    ...
}
```

Notice that `CreatedAtAction(nameof(Me))` creates a response header:
`Location: /api/Identity/me`
However, the `Me` action is protected by `[Authorize]`. A user who just called `POST /api/Identity/register` does not possess a JWT bearer token yet (registration only returns `{ userId: "..." }`).

## 2. Blast Radius & Defect Reproduction
1. Anonymous user registers via `POST /api/Identity/register`.
2. Response is `201 Created` with `Location: /api/Identity/me`.
3. Standard HTTP clients (e.g. mobile apps, automated API scrapers, curl with `--location`) immediately issue a GET to `/api/Identity/me`.
4. The request fails with `401 Unauthorized` because the client has not logged in or acquired a JWT token yet.

## 3. Affected Files
- [`src/API/CrowdFunding.API/Controllers/IdentityController.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/API/CrowdFunding.API/Controllers/IdentityController.cs#L70)

## 4. Recommended Fix (Greenfield)
Two clean greenfield options:
1. **Option A (Recommended for modern APIs)**: Modify `RegisterUserCommandHandler` to also issue a JWT token on registration and return both `UserId` and `Token` so the user is immediately logged in upon successful registration.
2. **Option B**: Return `CreatedAtAction(nameof(GetById), new { id = result.UserId }, ...)` or a standard `201 Created` without pointing to `/me`.

---

## Resolution Note (doc reconciliation pass)

This ticket's fix already landed in commit `bdfb810` earlier in this session's branch history; the `Status` field above was not updated at the time. Verified against current code during the TICKET-036/037/039 follow-up audit (2026-09-08) — the described defect no longer reproduces.

# QA Ticket: TICKET-002

**Title:** Race Condition in First-User Registration Grants Multiple Unintended Administrator Accounts  
**Severity:** 🔴 P1 (High - Security & Privilege Escalation)  
**QA Focus Area:** Application Security & Concurrency QA  
**Found By:** `qa-security-auth` / `qa-concurrency-financial`  
**Status:** Open  
**Project Mode:** Greenfield (No backward compatibility required)  

---

## 1. Description
In `RegisterUserCommandHandler`, the application attempts to bootstrap the initial administrator by checking if any users currently exist in the database:

```csharp
if (!await _userRepository.AnyAsync(cancellationToken))
{
    user.AssignRole(RoleConstants.Admin);
}
else
{
    user.AssignRole(RoleConstants.Creator);
    user.AssignRole(RoleConstants.Backer);
}

await _userRepository.AddAsync(user, cancellationToken);
```

Because `_userRepository.AnyAsync` and `_userRepository.AddAsync` are not wrapped in a database transaction with table-level locking or an advisory lock, this check is subject to a classic TOCTOU (Time-of-Check to Time-of-Use) race condition.

## 2. Blast Radius & Defect Reproduction
1. Database is newly deployed with zero users.
2. Attacker sends two (or more) simultaneous `POST /api/Identity/register` requests with different email addresses (`userA@test.com` and `userB@test.com`).
3. Both threads execute `_userRepository.AnyAsync` concurrently. Both receive `false`.
4. Both threads execute `user.AssignRole(RoleConstants.Admin)`.
5. Both transactions save changes.
6. **Outcome:** Both users are provisioned as full system Administrators with permissions `identity:roles:assign`, `identity:permissions:grant`, `campaigns:manage:any`, and `moderation:review`.

## 3. Affected Files
- [`src/Modules/Identity/CrowdFunding.Modules.Identity.Application/Features/Users/Commands/RegisterUser/RegisterUserCommandHandler.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Identity/CrowdFunding.Modules.Identity.Application/Features/Users/Commands/RegisterUser/RegisterUserCommandHandler.cs#L46-L55)

## 4. Recommended Fix (Greenfield)
Since this is a greenfield project:
1. Remove automatic runtime first-user admin bootstrapping from the public HTTP registration endpoint entirely.
2. Seed the initial administrator account via database migration or dedicated CLI bootstrap command (e.g. `dotnet run -- seed-admin`).
3. Ensure the public `RegisterUserCommandHandler` *always* assigns standard non-privileged roles (`RoleConstants.Creator` and `RoleConstants.Backer`).

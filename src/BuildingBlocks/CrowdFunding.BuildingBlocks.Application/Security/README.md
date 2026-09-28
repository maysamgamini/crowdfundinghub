# Security

## Purpose
Contains shared security contracts used by handlers to inspect the current user and enforce authorization.

## Files
- `ForbiddenAccessException.cs`: Thrown when an authenticated caller lacks the permission or ownership an operation needs. Mapped to HTTP 403.
- `ICurrentUser.cs`: The authenticated caller as handlers see it (user id from the JWT `sub` claim, email, roles, permissions).
- `IRequestContext.cs`: Transport details of the current request (client IP address and user agent), recorded by the audit-logging behavior.
- `UnauthenticatedException.cs`: Thrown when an operation requires an authenticated caller and there is none. Mapped to HTTP 401. Used instead of the BCL `UnauthorizedAccessException`, which the runtime throws for file-system permission failures.

## Child Folders
- None.

# Application Exceptions

## Purpose
Defines technology-agnostic exception types thrown by domain or transaction boundaries and consumed by application handlers or API middleware without leaking infrastructure references.

## Files
- `ConcurrencyConflictException.cs`: Thrown when optimistic concurrency token validation fails (such as PostgreSQL `xmin` concurrency conflicts in EF Core). Abstracted from `DbUpdateConcurrencyException` to enable clean decoupling between application logic and database persistence libraries. Mapped to HTTP 409 Conflict by `GlobalExceptionHandler`.
- `ResourceConflictException.cs`: Thrown when a command conflicts with the current business state of a resource (such as attempting duplicate registrations or illegal state-machine transitions). Mapped to HTTP 409 Conflict by `GlobalExceptionHandler` conforming to RFC 9110 §15.5.10.
- `NotFoundException.cs`: Thrown when a requested resource does not exist. Mapped to HTTP 404. Used instead of the BCL `KeyNotFoundException`, which any dictionary lookup can throw.
- `RequestValidationException.cs`: Thrown by `ValidationPipelineBehavior` with the per-property errors of a failed FluentValidation run. Mapped to HTTP 400 with an `errors` member.

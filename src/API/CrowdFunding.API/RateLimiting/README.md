# Rate Limiting Configuration

## Purpose
Configures partitioned rate limiting policies to protect authentication and contribution endpoints against brute-force attacks, denial-of-service, Sybil registration storms, and card testing.

## Files
- `RateLimitingConfiguration.cs`: Defines rate limiting partitions, permit windows, and HTTP 429 response handling using ASP.NET Core's built-in `RateLimiter` middleware.

## Policies
1. **`auth-strict` (`RateLimitingConfiguration.AuthPolicy`)**:
   - **Target Endpoints**: `/api/identity/login`, `/api/identity/register`.
   - **Partitioning**: Partitioned by client IP (`RemoteIpAddress`).
   - **Threshold**: 10 permits per 1-minute fixed window.
   - **Purpose**: Mitigates brute-force credential stuffing and mass automated account creation.

2. **`payment-strict` (`RateLimitingConfiguration.PaymentPolicy`)**:
   - **Target Endpoints**: `/api/contributions`, `/api/contributions/{id}/confirm-payment`.
   - **Partitioning**: Partitioned by authenticated user identifier (`ClaimTypes.NameIdentifier`), falling back to remote IP if unauthenticated.
   - **Threshold**: 20 permits per 1-minute fixed window.
   - **Purpose**: Mitigates automated pledge flooding and credit card testing.

## Rejection Behavior
When request volume exceeds the allocated window quota, the middleware halts pipeline execution and returns an HTTP status code `429 Too Many Requests`.

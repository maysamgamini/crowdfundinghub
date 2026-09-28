# Rate Limiting Configuration

## Purpose
Protects the API from abusive callers with ASP.NET Core's built-in rate limiter: one global per-caller limit plus stricter named policies for authentication, payment and webhook routes.

## Files
- `RateLimitingConfiguration.cs`: Registers the global limiter, the named policies and the 429 response writer.

## Partitioning
- Callers are keyed by the authenticated JWT `sub` claim (`user:{sub}`), falling back to the client IP (`ip:{address}`) for anonymous requests.
- This depends on middleware order in `Program.cs`: `UseForwardedHeaders` first (the IP is the real client behind a trusted proxy, not the proxy), then `UseAuthentication`, then `UseRateLimiter`.

## Limits (fixed windows, configurable under `RateLimiting:{Section}:PermitLimit|WindowSeconds`)
| Policy | Applies to | Partition | Default |
| :--- | :--- | :--- | :--- |
| Global limiter | Every endpoint except `/health/*` and `/.well-known/jwks.json` | `sub`, else IP | 300 / 60 s |
| `auth-strict` (`AuthPolicy`) | Register, login, refresh | IP | 10 / 60 s |
| `payment-strict` (`PaymentPolicy`) | Contribution and payment endpoints | `sub`, else IP | 20 / 60 s |
| `webhook-strict` (`WebhookPolicy`) | Payment gateway and media-analysis webhooks | IP | 200 / 60 s |

The webhook limit is deliberately generous: gateways send from shared, rotating IP pools and the real defense is the HMAC signature check.

## Rejections
A rejected request gets `429 Too Many Requests` as RFC 9457 `application/problem+json` with a `traceId`, and a `Retry-After` header when the limiter can compute it.

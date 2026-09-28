# API Security & Identity Integration

## Purpose
Bridges ASP.NET Core authentication and request infrastructure into the application layer's abstractions (`ICurrentUser`, `IRequestContext`), hosts the public key discovery endpoint for token verification, configures the edge (CORS, forwarded headers), and verifies the signatures of inbound webhooks.

## Files
- `HttpContextCurrentUser.cs`: Adapts the ASP.NET Core `IHttpContextAccessor` into the `ICurrentUser` interface defined in `BuildingBlocks.Application.Security`. Reads the user id from the JWT `sub` claim only, plus `email`, `role` and `permission` claims (names in `CustomClaimTypes`; inbound claim mapping is off, so the names on the wire are the names in code).
- `EdgeSecurityConfiguration.cs`: `AddCrowdFundingCors` registers the default CORS policy from `Cors:AllowedOrigins` (explicit list, credentials allowed for SignalR, no wildcard; empty means no cross-origin calls). `AddCrowdFundingForwardedHeaders` trusts `X-Forwarded-For`/`X-Forwarded-Proto` only from loopback plus `ForwardedHeaders:KnownProxies` (IPs) and `ForwardedHeaders:KnownNetworks` (CIDR), so clients cannot spoof their IP past the per-IP rate limits.
- `HttpRequestContext.cs`: Adapts the HTTP context into `IRequestContext` (client IP address and user agent), used by the audit-logging behavior.
- `StripeWebhookSignatureVerifier.cs`: Verifies the `Stripe-Signature` header (`t=<unix-seconds>,v1=<hex HMAC-SHA256>`) against the raw request body, with a timestamp tolerance window against replays.
- `PaymentGatewayWebhookOptions.cs`: Binds the `PaymentGatewayWebhook` configuration section (the shared secret for payment webhooks).
- `CloudFunctionSignatureVerifier.cs`: Verifies the `X-Cloud-Signature: sha256=<hex HMAC-SHA256>` header of the inbound media-analysis webhook against the raw request body.
- `CloudFunctionWebhookOptions.cs`: Binds the `CloudFunctionWebhook` configuration section (the shared secret for media-analysis callbacks).
- `JwksEndpoint.cs`: Serves the public JSON Web Key Set (JWKS) endpoint at `/.well-known/jwks.json`. Discovers active and rotated ECDSA public keys from `ISigningKeyStore` without revealing private keys, allowing downstream microservices, gateways, or single-page applications to cryptographically verify JWT access tokens independently.

## Authentication Model
- **Token Format**: Asymmetric ES256 (ECDSA using P-256 and SHA-256).
- **Claims**: `sub` (the only identity claim), `email`, `display_name`, `security_stamp`, one `role` per role and one `permission` per permission. `MapInboundClaims = false`, `NameClaimType = sub`, `RoleClaimType = role`.
- **Private keys at rest**: the ES256 private keys in `identity_signing_keys` are AES-256-GCM encrypted by `SigningKeyProtector` (Identity.Infrastructure) with the key from `SigningKeyEncryption:Key`; startup fails without it.
- **Issuer Validation**: Enforced via `JwtBearerOptions` configured with strict issuer, audience, and lifetime validation (zero clock skew).
- **Dynamic Key Resolution**: `IssuerSigningKeyResolver` resolves verification keys in real-time from `ISigningKeyStore` based on the JWT `kid` header parameter, supporting zero-downtime key rotation.

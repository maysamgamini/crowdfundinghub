# API Security & Identity Integration

## Purpose
Bridges ASP.NET Core authentication and claims infrastructure into the application layer's domain abstractions (`ICurrentUser`), and hosts public cryptographic discovery endpoints for token verification.

## Files
- `HttpContextCurrentUser.cs`: Adapts the ASP.NET Core `IHttpContextAccessor` into the `ICurrentUser` interface defined in `BuildingBlocks.Application.Security`. Extracts the user ID (`ClaimTypes.NameIdentifier`), email, assigned roles (`ClaimTypes.Role`), and fine-grained permissions (`permission` claims).
- `JwksEndpoint.cs`: Serves the public JSON Web Key Set (JWKS) endpoint at `/.well-known/jwks.json`. Discovers active and rotated ECDSA public keys from `ISigningKeyStore` without revealing private keys, allowing downstream microservices, gateways, or single-page applications to cryptographically verify JWT access tokens independently.

## Authentication Model
- **Token Format**: Asymmetric ES256 (ECDSA using P-256 and SHA-256).
- **Issuer Validation**: Enforced via `JwtBearerOptions` configured with strict issuer, audience, and lifetime validation (zero clock skew).
- **Dynamic Key Resolution**: `IssuerSigningKeyResolver` resolves verification keys in real-time from `ISigningKeyStore` based on the JWT `kid` header parameter, supporting zero-downtime key rotation.

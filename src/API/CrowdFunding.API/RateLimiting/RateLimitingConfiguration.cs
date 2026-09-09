using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace CrowdFunding.API.RateLimiting;

/// <summary>
/// Rate limiting policies (improvement.md §2.8/§3.8) — previously absent everywhere, leaving
/// login, registration, and contribution endpoints open to brute-force credential attacks, Sybil
/// account creation, and payment-card testing.
/// </summary>
public static class RateLimitingConfiguration
{
    public const string AuthPolicy = "auth-strict";
    public const string PaymentPolicy = "payment-strict";
    public const string WebhookPolicy = "webhook-strict";

    /// <summary>
    /// Registers IP- and user-partitioned rate limiting policies for authentication and payment routes.
    /// Limits/windows are configurable (<c>RateLimiting:Auth:*</c>/<c>RateLimiting:Payment:*</c>) so
    /// integration tests running many requests through a single simulated client/IP can raise them,
    /// without changing the hardcoded production defaults below.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddCrowdFundingRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var authPermitLimit = configuration.GetValue("RateLimiting:Auth:PermitLimit", 10);
        var authWindowSeconds = configuration.GetValue("RateLimiting:Auth:WindowSeconds", 60);
        var paymentPermitLimit = configuration.GetValue("RateLimiting:Payment:PermitLimit", 20);
        var paymentWindowSeconds = configuration.GetValue("RateLimiting:Payment:WindowSeconds", 60);
        var webhookPermitLimit = configuration.GetValue("RateLimiting:Webhook:PermitLimit", 200);
        var webhookWindowSeconds = configuration.GetValue("RateLimiting:Webhook:WindowSeconds", 60);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Partitioned per client IP: login/registration are unauthenticated, so IP is the
            // only identity available to key the limiter on.
            options.AddPolicy(AuthPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = authPermitLimit,
                    Window = TimeSpan.FromSeconds(authWindowSeconds),
                }));

            // Contribution endpoints are authenticated, so partition per user when available and
            // fall back to IP only for the rare case a request reaches this policy unauthenticated.
            options.AddPolicy(PaymentPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = paymentPermitLimit,
                    Window = TimeSpan.FromSeconds(paymentWindowSeconds),
                }));

            // Anonymous, high-volume, gateway-originated calls (Stripe et al.) — neither IP nor
            // user identity is a safe partition key (the sender is a shared, rotating pool of
            // gateway IPs, and there is no authenticated user), so this policy is kept
            // deliberately generous and its real defense is the HMAC signature check inside the
            // handler, not this limiter. It exists only to bound worst-case abuse of a publicly
            // reachable, unauthenticated endpoint.
            options.AddPolicy(WebhookPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = webhookPermitLimit,
                    Window = TimeSpan.FromSeconds(webhookWindowSeconds),
                }));
        });

        return services;
    }
}

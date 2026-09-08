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

    public static IServiceCollection AddCrowdFundingRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Partitioned per client IP: login/registration are unauthenticated, so IP is the
            // only identity available to key the limiter on.
            options.AddPolicy(AuthPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                }));

            // Contribution endpoints are authenticated, so partition per user when available and
            // fall back to IP only for the rare case a request reaches this policy unauthenticated.
            options.AddPolicy(PaymentPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20,
                    Window = TimeSpan.FromMinutes(1),
                }));
        });

        return services;
    }
}

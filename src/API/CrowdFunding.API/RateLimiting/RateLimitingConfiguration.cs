using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CrowdFunding.API.RateLimiting;

/// <summary>
/// Rate limiting: a global per-caller limit on every endpoint plus stricter named policies for
/// authentication, payment and webhook routes. Rejections are RFC 9457 ProblemDetails (429) with
/// a <c>traceId</c> and, when the limiter knows it, a <c>Retry-After</c> header.
/// </summary>
/// <remarks>
/// <para>
/// Callers are partitioned by the authenticated JWT <c>sub</c>, falling back to the client IP for
/// anonymous requests (<see cref="UserOrIpPartitionKey"/>). That only works because the pipeline
/// runs <c>UseAuthentication</c> before <c>UseRateLimiter</c>, and <c>UseForwardedHeaders</c>
/// before both, so the IP is the real client behind the reverse proxy, not the proxy.
/// </para>
/// <para>
/// Limits and windows are configurable (<c>RateLimiting:{Global|Auth|Payment|Webhook}:{PermitLimit|WindowSeconds}</c>)
/// so integration tests that send many requests from one address can raise them without
/// changing the production defaults below.
/// </para>
/// </remarks>
public static class RateLimitingConfiguration
{
    /// <summary>Login, registration and token refresh: per IP (the caller is not yet authenticated).</summary>
    public const string AuthPolicy = "auth-strict";

    /// <summary>Contribution and payment endpoints: per user, falling back to IP.</summary>
    public const string PaymentPolicy = "payment-strict";

    /// <summary>Anonymous gateway webhooks: per IP, deliberately generous.</summary>
    public const string WebhookPolicy = "webhook-strict";

    /// <summary>
    /// Registers the global limiter, the named policies and the 429 ProblemDetails writer.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddCrowdFundingRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var global = ReadWindow(configuration, "Global", defaultPermitLimit: 300);
        var auth = ReadWindow(configuration, "Auth", defaultPermitLimit: 10);
        var payment = ReadWindow(configuration, "Payment", defaultPermitLimit: 20);
        var webhook = ReadWindow(configuration, "Webhook", defaultPermitLimit: 200);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRejectionAsync;

            // Applies to every request in addition to any named policy: bounds what one caller
            // can cost the platform regardless of which endpoint it hammers.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(UserOrIpPartitionKey(httpContext), _ => global));

            // Login and registration are unauthenticated, so the IP is the only key available.
            options.AddPolicy(AuthPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(IpPartitionKey(httpContext), _ => auth));

            options.AddPolicy(PaymentPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(UserOrIpPartitionKey(httpContext), _ => payment));

            // Anonymous, gateway-originated calls (Stripe et al.) from a shared, rotating pool of
            // IPs with no user identity. Kept generous: the real defense is the HMAC signature
            // check in the handler; this only bounds worst-case abuse of a public endpoint.
            options.AddPolicy(WebhookPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(IpPartitionKey(httpContext), _ => webhook));
        });

        return services;
    }

    /// <summary>
    /// The authenticated caller's <c>sub</c> claim, or the client IP for anonymous requests.
    /// Prefixed so a user id can never collide with an IP partition.
    /// </summary>
    /// <param name="httpContext">The current request.</param>
    /// <returns>The partition key.</returns>
    public static string UserOrIpPartitionKey(HttpContext httpContext)
    {
        var subject = httpContext.User.FindFirstValue(CustomClaimTypes.Subject);
        return string.IsNullOrEmpty(subject) ? IpPartitionKey(httpContext) : $"user:{subject}";
    }

    private static string IpPartitionKey(HttpContext httpContext)
        => $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

    private static FixedWindowRateLimiterOptions ReadWindow(IConfiguration configuration, string section, int defaultPermitLimit) => new()
    {
        PermitLimit = configuration.GetValue($"RateLimiting:{section}:PermitLimit", defaultPermitLimit),
        Window = TimeSpan.FromSeconds(configuration.GetValue($"RateLimiting:{section}:WindowSeconds", 60)),
        QueueLimit = 0,
    };

    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            httpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too Many Requests",
            Detail = "Rate limit exceeded. Retry after the interval in the Retry-After header.",
            Instance = httpContext.Request.Path,
        };
        problemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToHexString() ?? httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);
    }
}

using System.Net;
using CrowdFunding.API.Observability;
using Microsoft.AspNetCore.HttpOverrides;

namespace CrowdFunding.API.Security;

/// <summary>
/// Edge concerns configured from settings: which browser origins may call the API (CORS) and
/// which reverse proxies are trusted to report the client IP and scheme (forwarded headers).
/// </summary>
public static class EdgeSecurityConfiguration
{
    /// <summary>
    /// Registers the default CORS policy from <c>Cors:AllowedOrigins</c>. An empty list allows no
    /// cross-origin browser calls at all; there is deliberately no wildcard fallback.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddCrowdFundingCors(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options => options.AddDefaultPolicy(policy =>
        {
            policy
                .WithOrigins(allowedOrigins)
                .WithMethods(HttpMethods.Get, HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete)
                .WithHeaders("Authorization", "Content-Type", CorrelationIdMiddleware.CorrelationHeader, "X-Requested-With", "X-SignalR-User-Agent")
                .WithExposedHeaders(CorrelationIdMiddleware.CorrelationHeader, "Location", "Retry-After")
                // The SignalR JavaScript client negotiates with credentials enabled; allowed only
                // because the origins above are an explicit list, never "*".
                .AllowCredentials();
        }));

        return services;
    }

    /// <summary>
    /// Honors <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c> only from trusted proxies:
    /// loopback by default, plus <c>ForwardedHeaders:KnownProxies</c> (IP addresses) and
    /// <c>ForwardedHeaders:KnownNetworks</c> (CIDR ranges). Trusting every sender would let any
    /// client spoof its IP and walk past the per-IP rate limits.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddCrowdFundingForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        var knownProxies = configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [];
        var knownNetworks = configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [];

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            foreach (var proxy in knownProxies)
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }

            foreach (var network in knownNetworks)
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });

        return services;
    }
}

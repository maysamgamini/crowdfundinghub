using System.Net;
using System.Security.Claims;
using System.Text.Json;
using CrowdFunding.API.RateLimiting;
using CrowdFunding.API.Security;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// The API's edge configuration (rate limiting, CORS) exercised through the real middleware on a
/// minimal in-memory host, so limits can be set low without affecting the shared API fixture.
/// </summary>
public sealed class EdgeHardeningTests
{
    [Fact]
    public async Task RejectedRequest_ShouldBe429ProblemDetails_WithTraceIdAndRetryAfter()
    {
        await using var app = await StartAsync(new() { ["RateLimiting:Auth:PermitLimit"] = "1" });
        var client = app.GetTestClient();

        (await client.GetAsync("/auth")).EnsureSuccessStatusCode();
        var rejected = await client.GetAsync("/auth");

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        Assert.True(rejected.Headers.RetryAfter is not null);

        using var body = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
        Assert.Equal(429, body.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(body.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task PaymentPolicy_ShouldPartitionAuthenticatedCallersBySub_NotByTheirSharedIp()
    {
        await using var app = await StartAsync(new() { ["RateLimiting:Payment:PermitLimit"] = "1" });
        var client = app.GetTestClient();

        // Two users behind the same address each get their own budget; a repeat by one is limited.
        Assert.Equal(HttpStatusCode.OK, (await SendAsUserAsync(client, "user-a")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsUserAsync(client, "user-b")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await SendAsUserAsync(client, "user-a")).StatusCode);
    }

    [Fact]
    public void PartitionKey_ShouldPreferSub_AndFallBackToIp()
    {
        var anonymous = new DefaultHttpContext();
        anonymous.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");

        var authenticated = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(CustomClaimTypes.Subject, "42")], "test")),
        };
        authenticated.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");

        Assert.Equal("ip:203.0.113.7", RateLimitingConfiguration.UserOrIpPartitionKey(anonymous));
        Assert.Equal("user:42", RateLimitingConfiguration.UserOrIpPartitionKey(authenticated));
    }

    [Fact]
    public async Task Cors_ShouldAllowOnlyConfiguredOrigins()
    {
        await using var app = await StartAsync(new() { ["Cors:AllowedOrigins:0"] = "https://app.example.com" });
        var client = app.GetTestClient();

        var allowed = await PreflightAsync(client, "https://app.example.com");
        var denied = await PreflightAsync(client, "https://evil.example.com");

        Assert.Equal("https://app.example.com", allowed.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.False(denied.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static Task<HttpResponseMessage> SendAsUserAsync(HttpClient client, string subject)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/payment");
        request.Headers.Add("X-Test-Sub", subject);
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> PreflightAsync(HttpClient client, string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/auth");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        return client.SendAsync(request);
    }

    private static async Task<WebApplication> StartAsync(Dictionary<string, string?> settings)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddCrowdFundingRateLimiting(builder.Configuration);
        builder.Services.AddCrowdFundingCors(builder.Configuration);

        var app = builder.Build();
        app.UseCors();

        // Stands in for JWT authentication: the header becomes the caller's "sub" claim.
        app.Use((context, next) =>
        {
            if (context.Request.Headers.TryGetValue("X-Test-Sub", out var subject))
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(CustomClaimTypes.Subject, subject.ToString())], "test"));
            }

            return next(context);
        });

        app.UseRateLimiter();
        app.MapGet("/auth", () => "ok").RequireRateLimiting(RateLimitingConfiguration.AuthPolicy);
        app.MapGet("/payment", () => "ok").RequireRateLimiting(RateLimitingConfiguration.PaymentPolicy);

        await app.StartAsync();
        return app;
    }
}

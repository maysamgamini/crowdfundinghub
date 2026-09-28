using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace CrowdFunding.API.Documentation;

/// <summary>
/// OpenAPI 3.1 document generation with the built-in <c>Microsoft.AspNetCore.OpenApi</c> and the
/// Scalar API reference UI (replacing Swashbuckle, as apps/backend does).
/// </summary>
/// <remarks>
/// Summaries and descriptions come from the XML documentation comments (the .NET 10 OpenAPI
/// source generator reads them at build time). The document is served at
/// <c>/openapi/v1.json</c> and the UI at <c>/scalar/v1</c>, in Development and Testing only.
/// </remarks>
public static class OpenApiConfiguration
{
    /// <summary>The API title.</summary>
    public const string ApiTitle = "CrowdFunding Hub API";

    /// <summary>The OpenAPI document name and version.</summary>
    public const string DocumentName = "v1";

    /// <summary>The name of the JWT bearer security scheme.</summary>
    public const string SecuritySchemeName = "Bearer";

    private static readonly (string Name, string Description)[] Tags =
    [
        ("Identity", "User registration, authentication, asymmetric ES256 JWT access tokens, refresh token rotation, roles and permissions."),
        ("Campaigns", "Campaign creation, discovery, and lifecycle operations (publish, cancel)."),
        ("RewardTiers", "Limited-capacity reward tiers and slot reservations for backers."),
        ("Contributions", "Pledges to campaigns and their payment confirmation or failure."),
        ("Moderation", "Compliance reviews: approving or rejecting campaigns before publication."),
        ("CampaignUpdates", "Outbound HMAC-signed webhook subscriptions for campaign owners."),
        ("Notifications", "A user's notification preferences."),
        ("Webhooks", "Inbound, signature-verified callbacks from the payment gateway and the media-analysis function."),
        ("System", "Health probes and the public JWKS used to verify access tokens."),
    ];

    /// <summary>
    /// Registers the OpenAPI document with its metadata, tags and bearer security scheme.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddCrowdFundingOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(DocumentName, options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = ApiTitle,
                    Version = DocumentName,
                    Description = ApiDescription,
                };

                document.Tags = new HashSet<OpenApiTag>(Tags.Select(tag => new OpenApiTag { Name = tag.Name, Description = tag.Description }));

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[SecuritySchemeName] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Name = "Authorization",
                    Description = "ES256-signed access token from POST /api/identity/login.",
                };

                return Task.CompletedTask;
            });

            // Only operations that actually require authentication declare the bearer scheme, so
            // the document tells clients exactly which calls need a token.
            options.AddOperationTransformer((operation, context, _) =>
            {
                var metadata = context.Description.ActionDescriptor.EndpointMetadata;
                var requiresAuthentication = metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any();

                if (requiresAuthentication)
                {
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(SecuritySchemeName, context.Document)] = [],
                    });
                }

                return Task.CompletedTask;
            });
        });

        return services;
    }

    /// <summary>
    /// Maps the OpenAPI JSON document and the Scalar API reference UI.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The same endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapCrowdFundingOpenApi(this IEndpointRouteBuilder app)
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options => options
            .WithTitle(ApiTitle)
            .AddPreferredSecuritySchemes(SecuritySchemeName));

        return app;
    }

    private const string ApiDescription = """
        ## CrowdFunding Hub Modular Monolith API

        A Modular Monolith built with Clean Architecture and Domain-Driven Design. Each module
        (Identity, Campaigns, Contributions, Moderation, CampaignUpdates, Notifications) owns its
        data and communicates with the others through contracts and application events delivered
        by a transactional outbox.

        ### Authentication
        - Asymmetric **ES256** JWT bearer tokens. Public keys are published at `/.well-known/jwks.json` (RFC 7517).
        - Register or log in via `POST /api/identity/register` and `POST /api/identity/login`, then send
          `Authorization: Bearer {accessToken}`. Endpoints enforce permission policies such as `campaigns:create`.

        ### Errors (RFC 9457 Problem Details)
        Every error is `application/problem+json` with a `traceId`:
        400 validation (`errors` per property) or business rule violation, 401, 403, 404,
        409 state conflict or concurrency loss, 429 rate limited (`Retry-After`),
        503 database overloaded (`Retry-After`), 500 unexpected (no internal details).
        """;
}

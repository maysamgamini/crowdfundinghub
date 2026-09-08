using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace CrowdFunding.API.Documentation;

/// <summary>
/// Provides extension methods and setup for configuring Swagger/OpenAPI documentation and Swagger UI.
/// </summary>
public static class SwaggerConfiguration
{
    /// <summary>
    /// The API display title.
    /// </summary>
    public const string ApiTitle = "CrowdFunding Hub API";

    /// <summary>
    /// The current API version document name.
    /// </summary>
    public const string ApiVersion = "v1";

    /// <summary>
    /// The security scheme name for JWT Bearer authentication.
    /// </summary>
    public const string SecuritySchemeName = "Bearer";

    /// <summary>
    /// Adds Swagger/OpenAPI generation with XML comments, JWT security definitions, and module groupings.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddCrowdFundingSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(ConfigureSwaggerGen);
        return services;
    }

    /// <summary>
    /// Configures SwaggerGenOptions with OpenAPI info, XML comments, security, and tagging rules.
    /// </summary>
    /// <param name="options">The SwaggerGenOptions to configure.</param>
    public static void ConfigureSwaggerGen(SwaggerGenOptions options)
    {
        options.SwaggerDoc(ApiVersion, new OpenApiInfo
        {
            Title = ApiTitle,
            Version = ApiVersion,
            Description = """
                ## CrowdFunding Hub Modular Monolith API

                Welcome to the **CrowdFunding Hub API** documentation. This platform is architected as a clean **Modular Monolith** adhering to Domain-Driven Design (DDD) principles and Clean Architecture.

                ### Architecture Overview
                The platform is structured into distinct bounded contexts:
                - **Identity**: User registration, credential authentication, asymmetric ES256 cryptographic signing, role-based access control, and granular permissions.
                - **Campaigns**: Campaign lifecycle management (Draft, Published, Completed, Cancelled), funding goals, discovery, and search.
                - **Contributions**: Backer pledge processing, external payment gateway integration, and contribution status tracking.
                - **Moderation**: Administrative compliance reviews, campaign approvals, rejection workflows, and audit history.
                - **CampaignUpdates**: Creator updates and backer communications.
                - **Notifications**: Cross-module notification delivery driven by domain events.

                Inter-module communication is asynchronous and fully decoupled using domain events combined with the **Transactional Outbox Pattern** to ensure reliable delivery without distributed transactions.

                ### Authentication & Authorization
                - **Scheme**: Asymmetric **ES256 (ECDSA using P-256 and SHA-256)** JWT Bearer authentication.
                - **Public Key Discovery**: Active public signing keys are published at `/.well-known/jwks.json` (RFC 7517) for token verification by downstream gateways and microservices without exposing private keys.
                - **How to Test Secured Endpoints**:
                  1. Register or login via `POST /api/Identity/register` or `POST /api/Identity/login`.
                  2. Copy the `accessToken` string from the JSON response.
                  3. Click the **Authorize** button at the top right of this page.
                  4. Paste your token into the **Value** input field (do not prefix with `Bearer `; the scheme prefix is added automatically).
                  5. Click **Authorize** and then **Close**. Subsequent API requests from Swagger UI will automatically include your JWT bearer token.
                - **Granular Permissions**: Endpoints enforce fine-grained permission policies (e.g., `campaigns:create`, `campaigns:publish`, `moderation:review`, `identity:roles:assign`).

                ### Error Handling & RFC 9457 Problem Details
                All error and failure responses strictly adhere to the **RFC 9457 Problem Details** standard:
                - **400 Bad Request**: Input validation failures, returning structured property errors under `errors`.
                - **401 Unauthorized**: Missing, expired, or invalid Bearer authentication token.
                - **403 Forbidden**: Authenticated caller lacks the required permission policy.
                - **404 Not Found**: The requested resource was not found.
                - **409 Conflict**: Concurrency conflicts or resource state violations.
                - **429 Too Many Requests**: Rate limit policy threshold exceeded.
                - **500 Internal Server Error**: Unhandled exception captured by `GlobalExceptionHandler`.

                Every response includes a `correlationId` header and problem details property for distributed tracing.
                """,
            Contact = new OpenApiContact
            {
                Name = "CrowdFunding Hub Platform Team"
            }
        });

        // Wire in XML documentation files from API, module Contracts, and BuildingBlocks
        var xmlDocFiles = new[]
        {
            $"{typeof(SwaggerConfiguration).Assembly.GetName().Name}.xml",
            "CrowdFunding.Modules.Campaigns.Contracts.xml",
            "CrowdFunding.Modules.Contributions.Contracts.xml",
            "CrowdFunding.Modules.Identity.Contracts.xml",
            "CrowdFunding.Modules.Moderation.Contracts.xml",
            "CrowdFunding.BuildingBlocks.Application.xml"
        };

        foreach (var xmlFile in xmlDocFiles)
        {
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
            }
        }

        // Configure JWT Bearer Security Definition & Requirement
        options.AddSecurityDefinition(SecuritySchemeName, new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Description = "Enter JWT Bearer token: **{token}** (without 'Bearer ' prefix).",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        });

        options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecuritySchemeReference(SecuritySchemeName),
                new List<string>()
            }
        });

        // Document filter for module tag descriptions
        options.DocumentFilter<SwaggerTagDescriptionsDocumentFilter>();

        // Group endpoints by module tags (Identity, Campaigns, Contributions, Moderation, System)
        options.TagActionsBy(apiDesc =>
        {
            var tagMetadata = apiDesc.ActionDescriptor.EndpointMetadata.OfType<ITagsMetadata>().FirstOrDefault();
            if (tagMetadata?.Tags is { Count: > 0 })
            {
                return tagMetadata.Tags.ToArray();
            }

            if (apiDesc.ActionDescriptor is ControllerActionDescriptor controllerAction)
            {
                var controllerName = controllerAction.ControllerName;
                if (controllerName.Equals("Identity", StringComparison.OrdinalIgnoreCase))
                    return ["Identity"];
                if (controllerName.Equals("Campaigns", StringComparison.OrdinalIgnoreCase))
                    return ["Campaigns"];
                if (controllerName.Equals("Contributions", StringComparison.OrdinalIgnoreCase))
                    return ["Contributions"];
                if (controllerName.Equals("Moderation", StringComparison.OrdinalIgnoreCase))
                    return ["Moderation"];
                if (controllerName.Equals("WeatherForecast", StringComparison.OrdinalIgnoreCase))
                    return ["System"];

                return [controllerName];
            }

            var relativePath = apiDesc.RelativePath ?? string.Empty;
            if (relativePath.StartsWith("health", StringComparison.OrdinalIgnoreCase) ||
                relativePath.StartsWith(".well-known", StringComparison.OrdinalIgnoreCase))
            {
                return ["System"];
            }

            return ["System"];
        });

        options.OrderActionsBy(apiDesc =>
        {
            var tag = apiDesc.ActionDescriptor.EndpointMetadata.OfType<ITagsMetadata>().FirstOrDefault()?.Tags?.FirstOrDefault() ?? "System";
            return $"{tag}_{apiDesc.RelativePath}_{apiDesc.HttpMethod}";
        });
    }

    /// <summary>
    /// Enables Swagger and Swagger UI with deep linking, doc expansion, and request duration display.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder with Swagger UI configured.</returns>
    public static IApplicationBuilder UseCrowdFundingSwagger(this IApplicationBuilder app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint($"/swagger/{ApiVersion}/swagger.json", $"{ApiTitle} {ApiVersion}");
            options.DocumentTitle = ApiTitle;
            options.DocExpansion(DocExpansion.List);
            options.EnableDeepLinking();
            options.DisplayRequestDuration();
        });

        return app;
    }
}

using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CrowdFunding.API.Documentation;

/// <summary>
/// Enriches Swagger/OpenAPI documentation with explicit module tag descriptions and ordering.
/// </summary>
public sealed class SwaggerTagDescriptionsDocumentFilter : IDocumentFilter
{
    /// <inheritdoc/>
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        swaggerDoc.Tags ??= new HashSet<OpenApiTag>();

        var tags = new[]
        {
            new OpenApiTag
            {
                Name = "Identity",
                Description = "User authentication, registration, asymmetric ES256 JWT tokens, role management, and fine-grained permissions."
            },
            new OpenApiTag
            {
                Name = "Campaigns",
                Description = "Campaign creation, public discovery, goal tracking, and lifecycle operations (publishing, cancellation)."
            },
            new OpenApiTag
            {
                Name = "Contributions",
                Description = "Financial contributions and pledges to campaigns, payment gateway confirmation, and failure management."
            },
            new OpenApiTag
            {
                Name = "Moderation",
                Description = "Administrative content moderation, campaign compliance reviews, approvals, and rejection feedback."
            },
            new OpenApiTag
            {
                Name = "System",
                Description = "Platform health checks (liveness and readiness), public JWKS cryptographic key discovery, and diagnostics."
            }
        };

        foreach (var tag in tags)
        {
            if (!swaggerDoc.Tags.Any(t => string.Equals(t.Name, tag.Name, StringComparison.OrdinalIgnoreCase)))
            {
                swaggerDoc.Tags.Add(tag);
            }
        }
    }
}

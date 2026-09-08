# OpenAPI & Swagger Documentation

## Purpose
Contains OpenAPI (Swagger) document filters, schema enrichers, and metadata definitions that format the public HTTP API documentation.

## Files
- `SwaggerTagDescriptionsDocumentFilter.cs`: Implements Swashbuckle's `IDocumentFilter` to supply explicit descriptions and grouping for API tags (`Identity`, `Campaigns`, `Contributions`, `Moderation`, `System`) in generated OpenAPI specification documents.

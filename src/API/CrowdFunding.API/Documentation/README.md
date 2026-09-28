# OpenAPI Documentation

## Purpose
Generates the OpenAPI 3.1 description of the HTTP API with the built-in `Microsoft.AspNetCore.OpenApi` package and serves the Scalar API reference UI. Swashbuckle is no longer used.

## Files
- `OpenApiConfiguration.cs`: `AddCrowdFundingOpenApi` registers document `v1` with its title and description, the module tags with descriptions, the JWT bearer security scheme, and an operation transformer that attaches the security requirement only to operations that require authentication. `MapCrowdFundingOpenApi` maps `/openapi/v1.json` and the Scalar UI at `/scalar/v1`.

## Notes
- Operation summaries and parameter descriptions come from XML documentation comments, which the .NET 10 OpenAPI source generator reads at build time (`GenerateDocumentationFile` is on for every project).
- The document and UI are mapped in `Development` and `Testing` only.

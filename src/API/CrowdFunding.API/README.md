# Crowd Funding API

## Purpose
Entry-point ASP.NET Core application that wires authentication, modules, mapping, middleware, Swagger, and background processing.

## Files
- `appsettings.Development.json`: Configuration file used by the application or tooling.
- `appsettings.json`: Configuration file used by the application or tooling.
- `CrowdFunding.API.csproj`: Project file that defines dependencies, target framework, and assembly references for this area.
- `CrowdFunding.API.csproj.user`: Supporting source file for Crowd Funding API csproj.
- `CrowdFunding.API.http`: HTTP scratch file for manually exercising API endpoints during development.
- `Program.cs`: Application startup that configures services, middleware, authentication, authorization, and database migrations.
- `WeatherForecast.cs`: Template sample type left from the ASP.NET starter and useful as a placeholder example.

## Child Folders
- `Background`: Runs background infrastructure tasks such as polling module outboxes and publishing pending application events.
- `Contracts`: Contains request and response contracts returned by the HTTP API.
- `Controllers`: Hosts the REST controllers that translate HTTP requests into application commands and queries.
- `Mapping`: Stores Mapster registration code that maps API contracts to application commands, queries, and DTOs.
- `Middleware`: Contains ASP.NET Core middleware for cross-cutting request handling concerns.
- `Properties`: Contains local development launch settings used by tooling such as Visual Studio and dotnet run.
- `Security`: Bridges ASP.NET Core authentication data into the shared current-user abstraction consumed by application handlers.

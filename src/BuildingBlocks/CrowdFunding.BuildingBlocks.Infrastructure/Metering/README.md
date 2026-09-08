# Infrastructure Metering (OpenMeter)

## Purpose
Provides the infrastructure implementation of `IUsageMeteringClient` utilizing an HTTP client targeting the OpenMeter Cloud API, resilient pipeline handlers, and configuration bindings.

## Files
- `OpenMeterClient.cs`: Implements `IUsageMeteringClient` by posting JSON-serialized `CloudEvent` payloads to `/api/v1/events`. Uses resilience handlers and safely swallows transient transmission errors so business transactions remain unaffected.
- `OpenMeterOptions.cs`: Binds to the `"OpenMeter"` configuration section in `appsettings.json`, specifying the API base URL and bearer token / API key.
- `MeteringDependencyInjection.cs`: Dependency injection extension method (`AddOpenMeterMetering`) that wires `OpenMeterOptions`, `OpenMeterFeeOptions`, and registers `HttpClient<IUsageMeteringClient, OpenMeterClient>` configured with `Microsoft.Extensions.Http.Resilience`.

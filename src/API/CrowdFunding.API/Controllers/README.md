# Controllers

## Purpose
Hosts the REST controllers that translate HTTP requests into application commands and queries.

## Files
- `CampaignsController.cs`: ASP.NET Core controller that exposes the Campaigns API surface.
- `ContributionsController.cs`: ASP.NET Core controller that exposes the Contributions API surface.
- `IdentityController.cs`: ASP.NET Core controller that exposes the Identity API surface.
- `ModerationController.cs`: ASP.NET Core controller that exposes the Moderation API surface.
- `WeatherForecastController.cs`: ASP.NET Core controller that exposes the Weather Forecast API surface.

## Child Folders
- None.

## Notes
Controllers stay intentionally thin: they validate input, dispatch commands or queries, and map results back to HTTP contracts.

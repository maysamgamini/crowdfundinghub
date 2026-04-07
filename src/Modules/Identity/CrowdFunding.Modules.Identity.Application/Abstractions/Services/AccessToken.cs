namespace CrowdFunding.Modules.Identity.Application.Abstractions.Services;

public sealed record AccessToken(string Value, DateTime ExpiresAtUtc);

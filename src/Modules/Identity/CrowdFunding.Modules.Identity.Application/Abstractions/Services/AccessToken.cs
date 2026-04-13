namespace CrowdFunding.Modules.Identity.Application.Abstractions.Services;

/// <summary>
/// Represents a generated access token together with its expiration timestamp.
/// </summary>
public sealed record AccessToken(string Value, DateTime ExpiresAtUtc);

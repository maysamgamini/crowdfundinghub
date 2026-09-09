namespace CrowdFunding.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Defines configuration values used when generating JWT access tokens.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;

    // TICKET-036: shortened from 60 to 15 minutes. Short-lived access tokens plus Refresh Token
    // Rotation (RefreshTokenService/RefreshAccessTokenCommandHandler) bound the exposure window
    // of a leaked access token to at most this long, instead of an hour.
    public int ExpirationMinutes { get; init; } = 15;
    public int RefreshTokenExpirationDays { get; init; } = 30;
}

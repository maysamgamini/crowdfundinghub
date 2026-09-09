namespace CrowdFunding.Moderation.Service.Security;

/// <summary>
/// Points this extracted service at the Identity module's public key set, bound from
/// <c>Authentication:JwksUri</c>. There is no shared secret and no reference to the Identity
/// module's assemblies anywhere in this project — decentralized verification means this service
/// only ever holds public key material, fetched over HTTP from whichever host currently issues
/// tokens.
/// </summary>
public sealed class JwksOptions
{
    public const string SectionName = "Authentication";

    public string JwksUri { get; init; } = string.Empty;

    public string Issuer { get; init; } = string.Empty;
}

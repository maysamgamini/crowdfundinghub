namespace CrowdFunding.Modules.Identity.Application.Abstractions.Services;

/// <summary>
/// The fast-path (TICKET-036) distributed revocation blacklist that lets every module — none of
/// which query the Identity database on every request — reject an access token within
/// sub-second global propagation, despite JWTs otherwise being stateless and unrevocable until
/// natural expiration. Implementations only need to remember a revoked stamp for as long as an
/// access token embedding it could still pass signature/lifetime validation (i.e. the access
/// token TTL), since after that window the token would have expired on its own regardless.
/// </summary>
public interface ISecurityStampRevocationStore
{
    /// <summary>Publishes <paramref name="revokedStamp"/> so tokens carrying it are rejected.</summary>
    Task RevokeAsync(Guid userId, Guid revokedStamp, CancellationToken cancellationToken);

    /// <summary>Checks whether <paramref name="stamp"/> has been revoked for <paramref name="userId"/>.</summary>
    Task<bool> IsRevokedAsync(Guid userId, Guid stamp, CancellationToken cancellationToken);
}

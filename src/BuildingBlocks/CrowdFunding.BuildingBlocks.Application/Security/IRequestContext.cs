namespace CrowdFunding.BuildingBlocks.Application.Security;

/// <summary>
/// Exposes transport-level details of the in-flight request to application handlers/behaviors
/// (TICKET-039's audit logging needs the caller's IP address and User-Agent) without any
/// Application-layer code taking a direct dependency on ASP.NET Core's <c>HttpContext</c> — the
/// same separation <see cref="ICurrentUser"/> already establishes for identity.
/// </summary>
public interface IRequestContext
{
    string? IpAddress { get; }
    string? UserAgent { get; }
}

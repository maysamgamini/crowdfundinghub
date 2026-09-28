namespace CrowdFunding.BuildingBlocks.Application.Security;

/// <summary>
/// Thrown when a request needs an authenticated caller and has none, or when presented
/// credentials (password, refresh token) are invalid. Mapped to HTTP 401 Unauthorized.
/// </summary>
/// <remarks>
/// Replaces <see cref="UnauthorizedAccessException"/>, which the BCL throws for file-system and
/// OS permission failures. Mapping that type to 401 would report a server fault as a client
/// authentication problem.
/// </remarks>
public sealed class UnauthenticatedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnauthenticatedException"/> class.
    /// </summary>
    /// <param name="message">A description of why authentication failed (logged, not returned).</param>
    public UnauthenticatedException(string message)
        : base(message)
    {
    }
}

namespace CrowdFunding.BuildingBlocks.Application.Security;

/// <summary>
/// Thrown when an authenticated caller lacks the permission or ownership an operation needs.
/// Mapped to HTTP 403 Forbidden.
/// </summary>
public sealed class ForbiddenAccessException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ForbiddenAccessException"/> class.
    /// </summary>
    /// <param name="message">A description of the missing permission (logged, not returned).</param>
    public ForbiddenAccessException(string message)
        : base(message)
    {
    }
}

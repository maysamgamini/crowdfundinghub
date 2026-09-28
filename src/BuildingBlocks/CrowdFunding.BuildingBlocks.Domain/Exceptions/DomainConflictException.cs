namespace CrowdFunding.BuildingBlocks.Domain.Exceptions;

/// <summary>
/// Thrown when an operation is valid in general but not in the aggregate's current state:
/// publishing a campaign that is not a draft, reserving a slot in a sold-out tier, refunding a
/// contribution that was never confirmed. Mapped to HTTP 409 Conflict (RFC 9110 §15.5.10): the
/// same request could succeed against a different state, so it is not a malformed request.
/// </summary>
public sealed class DomainConflictException : DomainException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DomainConflictException"/> class.
    /// </summary>
    /// <param name="message">A client-safe description of the state conflict.</param>
    public DomainConflictException(string message)
        : base(message)
    {
    }
}

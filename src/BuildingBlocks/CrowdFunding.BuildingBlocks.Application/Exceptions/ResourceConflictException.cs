namespace CrowdFunding.BuildingBlocks.Application.Exceptions;

/// <summary>
/// Thrown when a request conflicts with the current state of a resource — a duplicate unique
/// value (e.g. an email already registered) or an invalid state-machine transition (e.g.
/// publishing an already-published campaign). Mapped to HTTP 409 Conflict by
/// GlobalExceptionHandler, distinguishing a resource-state conflict from a malformed request
/// (400) per RFC 9110 §15.5.10.
/// </summary>
public sealed class ResourceConflictException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceConflictException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the resource conflict.</param>
    public ResourceConflictException(string message)
        : base(message)
    {
    }
}

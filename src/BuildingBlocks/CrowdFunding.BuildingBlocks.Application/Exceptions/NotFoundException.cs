namespace CrowdFunding.BuildingBlocks.Application.Exceptions;

/// <summary>
/// Thrown by a handler when the resource a request names does not exist (or is not visible to
/// the caller). Mapped to HTTP 404 Not Found.
/// </summary>
/// <remarks>
/// Replaces <see cref="KeyNotFoundException"/>, which the BCL also throws from any dictionary
/// lookup. Mapping the BCL type to 404 would turn a programming error into a misleading "not
/// found" and echo the framework's message to the client.
/// </remarks>
public sealed class NotFoundException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotFoundException"/> class.
    /// </summary>
    /// <param name="message">A client-safe description of the missing resource.</param>
    public NotFoundException(string message)
        : base(message)
    {
    }
}

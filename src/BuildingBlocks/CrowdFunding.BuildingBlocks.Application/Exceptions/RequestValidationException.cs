namespace CrowdFunding.BuildingBlocks.Application.Exceptions;

/// <summary>
/// Thrown by <see cref="Validation.ValidationPipelineBehavior{TCommand,TResult}"/> when a command
/// fails its registered validators. Mapped to an RFC 9457 400 response whose <c>errors</c>
/// member lists the messages per property.
/// </summary>
/// <remarks>
/// Owned by this codebase rather than reusing FluentValidation's own exception, so the API layer
/// maps a stable application contract and never depends on the validation library's types.
/// </remarks>
public sealed class RequestValidationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RequestValidationException"/> class.
    /// </summary>
    /// <param name="errors">Validation messages keyed by property name.</param>
    public RequestValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    /// <summary>
    /// Gets the validation messages keyed by property name.
    /// </summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

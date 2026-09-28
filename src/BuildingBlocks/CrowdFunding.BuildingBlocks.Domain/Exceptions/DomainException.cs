namespace CrowdFunding.BuildingBlocks.Domain.Exceptions;

/// <summary>
/// Base type for every exception a domain model throws to reject an operation. Its message is
/// written for the caller, so the API returns it as the ProblemDetails <c>detail</c>.
/// </summary>
/// <remarks>
/// Domain code never throws <see cref="InvalidOperationException"/> or
/// <see cref="ArgumentException"/> to signal a business outcome: the framework, EF Core and
/// Npgsql throw those too, and their messages can carry schema names or connection details.
/// Keeping business rejections on a type the domain owns lets the API map exactly these to 4xx
/// and turn everything else into a generic 500.
/// </remarks>
public abstract class DomainException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DomainException"/> class.
    /// </summary>
    /// <param name="message">A client-safe description of the rule that rejected the operation.</param>
    protected DomainException(string message)
        : base(message)
    {
    }
}

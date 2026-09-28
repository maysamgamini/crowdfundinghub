namespace CrowdFunding.BuildingBlocks.Domain.Exceptions;

/// <summary>
/// Thrown when the input to a domain operation breaks an invariant, whatever state the aggregate
/// is in: a blank title, a negative amount, a currency mismatch. Mapped to HTTP 400 Bad Request.
/// </summary>
public sealed class BusinessRuleViolationException : DomainException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessRuleViolationException"/> class.
    /// </summary>
    /// <param name="message">A client-safe description of the broken rule.</param>
    public BusinessRuleViolationException(string message)
        : base(message)
    {
    }
}

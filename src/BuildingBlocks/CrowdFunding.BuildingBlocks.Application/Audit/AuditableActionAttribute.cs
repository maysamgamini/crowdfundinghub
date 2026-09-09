namespace CrowdFunding.BuildingBlocks.Application.Audit;

/// <summary>
/// Marks a command as always forensically audited (TICKET-039), regardless of the caller's role.
/// <see cref="AuditLoggingPipelineBehavior{TCommand,TResult}"/> also audits every command executed
/// by an Admin automatically — this attribute exists for commands worth auditing no matter who
/// calls them (e.g. cancelling a campaign), so that guarantee doesn't silently depend on the
/// caller happening to be an Admin.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AuditableActionAttribute : Attribute
{
    public string ActionName { get; }

    public AuditableActionAttribute(string actionName)
    {
        ActionName = actionName;
    }
}

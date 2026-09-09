using System.Text.Json;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;

namespace CrowdFunding.BuildingBlocks.Application.Audit;

/// <summary>
/// Automatically writes an <see cref="AuditRecord"/> for every command that either carries
/// <see cref="AuditableActionAttribute"/> or was executed by a caller in the "Admin" role
/// (TICKET-039) — without any command handler ever importing an audit abstraction itself.
/// Registered once as an open generic
/// (<c>services.AddScoped(typeof(ICommandPipelineBehavior&lt;,&gt;), typeof(AuditLoggingPipelineBehavior&lt;,&gt;))</c>),
/// it applies to every command dispatched through <see cref="ICommandDispatcher"/>.
///
/// The literal "Admin" role name is intentional, not a shortcut: <c>RoleConstants.Admin</c> lives
/// in <c>Identity.Contracts</c>, and BuildingBlocks must never depend on a specific module — every
/// module depends on BuildingBlocks, never the reverse (see ArchitectureTests). The two places
/// that need this value stay in sync by policy (both fixed built-in role names), the same way
/// <c>CustomClaimTypes.SecurityStamp</c>'s claim name string is independently known on both the
/// issuing and verifying sides of a module boundary elsewhere in this codebase.
///
/// The audit write runs only after the inner delegate completes successfully — a command
/// that threw never happened, so there is nothing to audit. A failure while writing the audit
/// record itself is deliberately allowed to propagate (see <see cref="IAuditStore"/>): letting the
/// underlying command silently "succeed" while its forensic trail failed to write would defeat
/// this ticket's entire compliance purpose.
/// </summary>
public sealed class AuditLoggingPipelineBehavior<TCommand, TResult> : ICommandPipelineBehavior<TCommand, TResult>
    where TCommand : class
{
    private const string AdminRoleName = "Admin";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ICurrentUser _currentUser;
    private readonly IRequestContext _requestContext;
    private readonly IAuditStore _auditStore;

    public AuditLoggingPipelineBehavior(ICurrentUser currentUser, IRequestContext requestContext, IAuditStore auditStore)
    {
        _currentUser = currentUser;
        _requestContext = requestContext;
        _auditStore = auditStore;
    }

    public async Task<TResult> HandleAsync(TCommand command, CommandHandlerDelegate<TResult> next, CancellationToken cancellationToken)
    {
        var result = await next();

        var auditableAttribute = typeof(TCommand).GetCustomAttributes(typeof(AuditableActionAttribute), inherit: false)
            .Cast<AuditableActionAttribute>()
            .FirstOrDefault();

        if (auditableAttribute is null && !_currentUser.IsInRole(AdminRoleName))
        {
            return result;
        }

        var record = new AuditRecord(
            Guid.NewGuid(),
            _currentUser.UserId,
            _currentUser.Email ?? "system",
            auditableAttribute?.ActionName ?? typeof(TCommand).Name,
            typeof(TCommand).Name,
            JsonSerializer.Serialize(command, SerializerOptions),
            _requestContext.IpAddress ?? "unknown",
            _requestContext.UserAgent ?? "unknown",
            DateTime.UtcNow);

        await _auditStore.RecordAsync(record, cancellationToken);

        return result;
    }
}

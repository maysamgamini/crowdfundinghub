namespace CrowdFunding.BuildingBlocks.Application.Messaging;

/// <summary>Invokes the next stage of the command pipeline — either another behavior, or the
/// command's own handler once every behavior has run.</summary>
public delegate Task<TResult> CommandHandlerDelegate<TResult>();

/// <summary>
/// A cross-cutting concern that wraps every command dispatched through <see cref="ICommandDispatcher"/>
/// (TICKET-039) — audit logging, but also anything else system-wide a future ticket needs
/// (metrics, authorization, retry policy) — without any individual command handler having to know
/// it exists. Register an open-generic implementation
/// (<c>services.AddScoped(typeof(ICommandPipelineBehavior&lt;,&gt;), typeof(YourBehavior&lt;,&gt;))</c>)
/// to have it apply to every command; <see cref="CommandDispatcher"/> resolves every registered
/// behavior for the command's own closed generic type and chains them, outermost-first in
/// registration order, around the handler.
/// </summary>
public interface ICommandPipelineBehavior<in TCommand, TResult>
    where TCommand : class
{
    Task<TResult> HandleAsync(TCommand command, CommandHandlerDelegate<TResult> next, CancellationToken cancellationToken);
}

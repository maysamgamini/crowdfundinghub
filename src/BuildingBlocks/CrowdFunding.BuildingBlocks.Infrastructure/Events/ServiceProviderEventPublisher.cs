using System.Collections.Concurrent;
using System.Reflection;
using CrowdFunding.BuildingBlocks.Application.Events;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Events;

/// <summary>
/// Publishes application events by resolving their handlers from the service provider.
/// </summary>
public sealed class ServiceProviderEventPublisher : IEventPublisher
{
    private static readonly ConcurrentDictionary<Type, MethodInfo> HandleMethods = new();
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceProviderEventPublisher"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve event handlers.</param>
    public ServiceProviderEventPublisher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public async Task PublishAsync(object notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var eventType = notification.GetType();
        var handlerType = typeof(IEventHandler<>).MakeGenericType(eventType);
        var handlers = _serviceProvider.GetServices(handlerType);
        var handleMethod = HandleMethods.GetOrAdd(
            handlerType,
            static type => type.GetMethod(nameof(IEventHandler<object>.Handle))
                           ?? throw new InvalidOperationException($"Handler type '{type.FullName}' does not expose Handle."));

        var context = _serviceProvider.GetService<InboxMessageContext>();
        var messageId = context?.MessageId;

        foreach (var handler in handlers)
        {
            var inbox = messageId is null ? null : ResolveInbox(handler!.GetType().Assembly);
            var consumer = handler!.GetType().FullName!;

            // A retried message skips the handlers that already finished on an earlier attempt.
            if (inbox is not null && await inbox.HasProcessedAsync(messageId!.Value, consumer, cancellationToken))
            {
                continue;
            }

            // A handler that is one transaction gets its inbox row written inside that transaction
            // by the module's executor; for every other handler the row is written after it ends.
            var recordInTransaction = inbox is not null && handler is IRecordsInboxInTransaction;
            if (context is not null)
            {
                context.ConsumerToRecordInTransaction = recordInTransaction ? consumer : null;
                context.RecordedInTransaction = false;
            }

            try
            {
                await InvokeAsync(handleMethod, handler, notification, cancellationToken);
            }
            catch (InboxAlreadyRecordedException)
            {
                continue;
            }
            finally
            {
                if (context is not null)
                {
                    context.ConsumerToRecordInTransaction = null;
                }
            }

            if (inbox is not null && !(context?.RecordedInTransaction ?? false))
            {
                await inbox.MarkProcessedAsync(messageId!.Value, consumer, DateTime.UtcNow, cancellationToken);
            }
        }
    }

    private IInbox? ResolveInbox(Assembly handlerAssembly)
    {
        var binding = _serviceProvider.GetServices<InboxBinding>()
            .FirstOrDefault(x => x.HandlerAssembly == handlerAssembly);

        return binding is null ? null : (IInbox)_serviceProvider.GetRequiredService(binding.InboxType);
    }

    // MethodInfo.Invoke wraps a handler's synchronous throw in TargetInvocationException; unwrap it
    // so callers see the handler's own exception whether it threw before or after its first await.
    private static async Task InvokeAsync(MethodInfo handleMethod, object handler, object notification, CancellationToken cancellationToken)
    {
        try
        {
            await (Task)handleMethod.Invoke(handler, [notification, cancellationToken])!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
        }
    }
}

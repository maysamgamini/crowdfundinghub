# Messaging

## Purpose
Contains the command and query dispatcher abstractions and their shared registration helpers.

## Files
- `CommandDispatcher.cs`: Dispatcher that routes command objects to their registered handlers.
- `DispatcherInvoker.cs`: Reflection-based helper that invokes strongly typed handlers.
- `ICommandDispatcher.cs`: Contract for sending a command to its handler through the pipeline behaviors.
- `ICommandHandler.cs`: Contract for a command handler (`ICommandHandler<TCommand, TResult>`).
- `ICommandPipelineBehavior.cs`: Contract for a behavior that wraps command handling (validation, audit logging); behaviors run in registration order, outermost first.
- `IMessageBus.cs`: Transport abstraction the outbox processors publish application events through (in-process by default, RabbitMQ publish-only as an option).
- `IQueryDispatcher.cs`: Contract for sending a query to its handler.
- `IQueryHandler.cs`: Contract for a query handler (`IQueryHandler<TQuery, TResult>`).
- `QueryDispatcher.cs`: Dispatcher that routes query objects to their registered handlers.
- `RequestHandlerRegistrationExtensions.cs`: `AddRequestHandlersFromAssemblies` scans assemblies and registers every command and query handler.

## Child Folders
- None.

## Notes
These abstractions are the glue that lets the API depend on application contracts instead of concrete handlers.

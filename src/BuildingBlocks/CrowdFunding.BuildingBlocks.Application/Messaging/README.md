# Messaging

## Purpose
Contains the command and query dispatcher abstractions and their shared registration helpers.

## Files
- `CommandDispatcher.cs`: Dispatcher that routes command objects to their registered handlers.
- `DispatcherInvoker.cs`: Reflection-based helper that invokes strongly typed handlers.
- `ICommandDispatcher.cs`: Supporting source file for I Command Dispatcher.
- `ICommandHandler.cs`: Application command handler for I.
- `IQueryDispatcher.cs`: Supporting source file for I Query Dispatcher.
- `IQueryHandler.cs`: Application query handler for I.
- `QueryDispatcher.cs`: Dispatcher that routes query objects to their registered handlers.
- `RequestHandlerRegistrationExtensions.cs`: Extension methods that encapsulate repeated registration or configuration logic.

## Child Folders
- None.

## Notes
These abstractions are the glue that lets the API depend on application contracts instead of concrete handlers.

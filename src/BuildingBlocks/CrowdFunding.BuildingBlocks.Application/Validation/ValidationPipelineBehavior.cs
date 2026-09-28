using CrowdFunding.BuildingBlocks.Application.Exceptions;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using FluentValidation;

namespace CrowdFunding.BuildingBlocks.Application.Validation;

/// <summary>
/// Runs every registered FluentValidation <see cref="IValidator{T}"/> for a command before its
/// handler, and throws <see cref="RequestValidationException"/> when any of them fails.
/// </summary>
/// <remarks>
/// Validation used to be repeated in every controller action (validate, copy errors into
/// ModelState, return ValidationProblem). As a pipeline behavior it runs for every dispatch path
/// the same way (HTTP endpoints, webhooks, event handlers, background services), and an endpoint
/// cannot forget it. Commands with no registered validator pass straight through.
/// </remarks>
/// <typeparam name="TCommand">The command type.</typeparam>
/// <typeparam name="TResult">The command result type.</typeparam>
public sealed class ValidationPipelineBehavior<TCommand, TResult> : ICommandPipelineBehavior<TCommand, TResult>
    where TCommand : class
{
    private readonly IValidator<TCommand>[] _validators;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationPipelineBehavior{TCommand,TResult}"/> class.
    /// </summary>
    /// <param name="validators">Every validator registered for <typeparamref name="TCommand"/>.</param>
    public ValidationPipelineBehavior(IEnumerable<IValidator<TCommand>> validators)
    {
        _validators = validators.ToArray();
    }

    /// <inheritdoc/>
    public async Task<TResult> HandleAsync(TCommand command, CommandHandlerDelegate<TResult> next, CancellationToken cancellationToken)
    {
        if (_validators.Length == 0)
        {
            return await next();
        }

        var context = new ValidationContext<TCommand>(command);
        var results = await Task.WhenAll(_validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));

        var errors = results
            .SelectMany(result => result.Errors)
            .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray(),
                StringComparer.Ordinal);

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }

        return await next();
    }
}

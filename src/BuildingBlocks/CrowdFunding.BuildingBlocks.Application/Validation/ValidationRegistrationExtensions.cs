using CrowdFunding.BuildingBlocks.Application.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.BuildingBlocks.Application.Validation;

/// <summary>
/// Registers <see cref="ValidationPipelineBehavior{TCommand,TResult}"/> for every command.
/// </summary>
public static class ValidationRegistrationExtensions
{
    /// <summary>
    /// Adds command validation to the dispatcher pipeline. Call it before any other behavior
    /// registration: behaviors wrap in registration order, so validation then runs outermost and
    /// an invalid command never reaches auditing or the handler.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddCommandValidation(this IServiceCollection services)
    {
        services.AddScoped(typeof(ICommandPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>));
        return services;
    }
}

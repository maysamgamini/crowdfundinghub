using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.AssignRoleToUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.GrantPermissionToUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.LoginUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.Logout;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.RefreshAccessToken;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.RegisterUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Queries.GetCurrentUser;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.Modules.Identity.Application.DependencyInjection;

/// <summary>
/// Registers services from the surrounding layer with the dependency injection container.
/// </summary>
public static class IdentityApplicationDependencyInjection
{
    /// <summary>
    /// Registers Identity application services, command handlers, and query handlers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddScoped<RegisterUserCommandHandler>();
        services.AddScoped<LoginUserCommandHandler>();
        services.AddScoped<RefreshAccessTokenCommandHandler>();
        services.AddScoped<LogoutCommandHandler>();
        services.AddScoped<AssignRoleToUserCommandHandler>();
        services.AddScoped<GrantPermissionToUserCommandHandler>();
        services.AddScoped<GetCurrentUserQueryHandler>();
        services.AddScoped<IValidator<RegisterUserCommand>, RegisterUserCommandValidator>();
        services.AddScoped<IValidator<LoginUserCommand>, LoginUserCommandValidator>();
        services.AddScoped<IValidator<RefreshAccessTokenCommand>, RefreshAccessTokenCommandValidator>();
        services.AddScoped<IValidator<LogoutCommand>, LogoutCommandValidator>();
        services.AddScoped<IValidator<AssignRoleToUserCommand>, AssignRoleToUserCommandValidator>();
        services.AddScoped<IValidator<GrantPermissionToUserCommand>, GrantPermissionToUserCommandValidator>();

        return services;
    }
}

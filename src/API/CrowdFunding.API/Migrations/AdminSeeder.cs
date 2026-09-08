using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.SeedAdmin;

namespace CrowdFunding.API.Migrations;

/// <summary>
/// CLI entry point for <c>dotnet run -- seed-admin &lt;email&gt; &lt;password&gt; &lt;displayName&gt;</c>, run once
/// by an operator out-of-band during deployment to create the initial administrator account.
/// Dispatches to SeedAdminCommandHandler (Identity.Application) rather than touching the
/// Identity domain directly, since the API project is not allowed to reference a module's Domain
/// layer (see IdentityModuleDependencyTests.Api_ShouldNotReference_IdentityDomainDirectly).
///
/// Replaces the previous "first user to register becomes Admin" bootstrap in
/// RegisterUserCommandHandler, which checked user-count via an unlocked AnyAsync — two
/// concurrent registrations against an empty database could both observe zero users and both be
/// granted Admin (a real privilege-escalation race, not just theoretical).
/// </summary>
public static class AdminSeeder
{
    public static async Task RunAsync(IServiceProvider services, string email, string password, string displayName, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AdminSeeder");
        var commandDispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();

        var result = await commandDispatcher.SendAsync<SeedAdminResult>(
            new SeedAdminCommand(email, password, displayName),
            cancellationToken);

        logger.LogInformation(
            result.WasNewlyCreated
                ? "Created new Administrator account '{Email}' ({UserId})."
                : "Ensured '{Email}' ({UserId}) has Administrator access — either already did, or was just promoted.",
            email, result.UserId);
    }
}

namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.SeedAdmin;

/// <summary>
/// Creates (or promotes) the initial Administrator account. Dispatched only from the
/// `dotnet run -- seed-admin` CLI path (see AdminSeeder in the API project) — never reachable
/// from a public HTTP endpoint. Public self-registration (RegisterUserCommandHandler) never
/// grants Admin.
/// </summary>
public sealed record SeedAdminCommand(string Email, string Password, string DisplayName);

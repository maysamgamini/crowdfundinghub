# Seed Admin Command

## Purpose
Orchestrates the creation or promotion of the system administrator user account out-of-band via operator CLI tooling, maintaining strict boundaries preventing administrative self-registration through the public HTTP API.

## Files
- `SeedAdminCommand.cs`: Command record containing administrative email, plain-text password, and display name. Dispatched only by `AdminSeeder` from the CLI.
- `SeedAdminCommandHandler.cs`: Command handler that checks if the specified user exists. If found, assigns `RoleConstants.Admin`; otherwise, creates the new user with hashed password and assigns the `Admin` role.
- `SeedAdminResult.cs`: Result record indicating the provisioned user ID and whether the account was newly registered or promoted.

## Security Context
Standard user registration (`RegisterUserCommand`) assigns only the `Creator` and `Backer` roles and never `Admin`, not even to the first user. `SeedAdminCommandHandler` is the only code that grants `RoleConstants.Admin`, and it is reachable only from the CLI (`dotnet run -- seed-admin <email> <password> <displayName>`).

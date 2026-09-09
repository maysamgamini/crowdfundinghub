using CrowdFunding.BuildingBlocks.Application.Exceptions;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Identity.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Identity.Application.Abstractions.Services;
using CrowdFunding.Modules.Identity.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.AssignRoleToUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.GrantPermissionToUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.LoginUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.Logout;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.RefreshAccessToken;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.RegisterUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.SeedAdmin;
using CrowdFunding.Modules.Identity.Application.Features.Users.Queries.GetCurrentUser;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using CrowdFunding.Modules.Identity.Domain.Aggregates;
using CrowdFunding.Modules.Identity.Domain.Entities;

namespace CrowdFunding.UnitTests;

public sealed class UserDomainTests
{
    [Fact]
    public void Register_ShouldNormalizeEmailAndDisplayName()
    {
        var user = User.Register(" creator@example.com ", " Creator ", "hash", DateTime.UtcNow);

        Assert.Equal("creator@example.com", user.Email);
        Assert.Equal("CREATOR@EXAMPLE.COM", user.NormalizedEmail);
        Assert.Equal("Creator", user.DisplayName);
    }

    [Fact]
    public void AssignRole_ShouldIgnoreDuplicateRoles()
    {
        var user = User.Register("user@example.com", "User", "hash", DateTime.UtcNow);

        user.AssignRole(RoleConstants.Creator);
        user.AssignRole(" creator ");

        Assert.Single(user.Roles);
    }

    [Fact]
    public void GrantPermission_ShouldIgnoreDuplicatePermissions()
    {
        var user = User.Register("user@example.com", "User", "hash", DateTime.UtcNow);

        user.GrantPermission(PermissionConstants.CampaignsCreate);
        user.GrantPermission($" {PermissionConstants.CampaignsCreate} ");

        Assert.Single(user.Permissions);
    }

    [Fact]
    public void Deactivate_ShouldMarkUserInactive()
    {
        var user = User.Register("user@example.com", "User", "hash", DateTime.UtcNow);

        user.Deactivate();

        Assert.False(user.IsActive);
    }

    [Fact]
    public void Register_ShouldThrow_WhenPasswordHashIsMissing()
    {
        var action = () => User.Register("user@example.com", "User", "   ", DateTime.UtcNow);

        var exception = Assert.Throws<ArgumentException>(action);

        Assert.Equal("Password hash is required. (Parameter 'passwordHash')", exception.Message);
    }

    [Fact]
    public void Register_ShouldThrow_WhenDisplayNameExceedsMaximumLength()
    {
        var action = () => User.Register("user@example.com", new string('a', 101), "hash", DateTime.UtcNow);

        var exception = Assert.Throws<ArgumentException>(action);

        Assert.Equal("Display name cannot exceed 100 characters. (Parameter 'displayName')", exception.Message);
    }

    [Fact]
    public void Register_ShouldThrow_WhenEmailIsMissingAtSymbol()
    {
        var action = () => User.Register("not-an-email", "User", "hash", DateTime.UtcNow);

        var exception = Assert.Throws<ArgumentException>(action);

        Assert.Equal("Email must be a valid email address. (Parameter 'email')", exception.Message);
    }
}

public sealed class RegisterUserCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldNeverAssignAdminRole_EvenToTheFirstUserInAnEmptyDatabase()
    {
        // Public self-registration must never grant Admin, including to the very first user —
        // that used to be decided by an unlocked AnyAsync check (RegisterUserCommandHandler),
        // which let two concurrent registrations against an empty database both observe zero
        // users and both become Administrators. The initial admin is now seeded out-of-band via
        // `dotnet run -- seed-admin` (AdminSeeder), never through this handler.
        var repository = new FakeUserRepository();
        var handler = new RegisterUserCommandHandler(
            new FakeIdentityDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)),
            new FakePasswordHasher(),
            repository,
            new FakeIdentityTransactionExecutor());

        var result = await handler.Handle(
            new RegisterUserCommand("admin@example.com", "Admin", "supersecret"),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.UserId);
        Assert.NotNull(repository.SavedUser);
        Assert.DoesNotContain(RoleConstants.Admin, repository.SavedUser!.Roles.Select(x => x.Role));
        Assert.Contains(RoleConstants.Creator, repository.SavedUser.Roles.Select(x => x.Role));
        Assert.Contains(RoleConstants.Backer, repository.SavedUser.Roles.Select(x => x.Role));
    }

    [Fact]
    public async Task Handle_ShouldAssignCreatorAndBackerRolesToLaterUsers()
    {
        var existingUser = User.Register("existing@example.com", "Existing", "hash", DateTime.UtcNow);
        var repository = new FakeUserRepository(existingUser);
        var handler = new RegisterUserCommandHandler(
            new FakeIdentityDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)),
            new FakePasswordHasher(),
            repository,
            new FakeIdentityTransactionExecutor());

        await handler.Handle(
            new RegisterUserCommand("creator@example.com", "Creator", "supersecret"),
            CancellationToken.None);

        Assert.NotNull(repository.SavedUser);
        Assert.Contains(RoleConstants.Creator, repository.SavedUser!.Roles.Select(x => x.Role));
        Assert.Contains(RoleConstants.Backer, repository.SavedUser.Roles.Select(x => x.Role));
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenEmailAlreadyExists()
    {
        var existingUser = User.Register("creator@example.com", "Creator", "hash", DateTime.UtcNow);
        var repository = new FakeUserRepository(existingUser);
        var handler = new RegisterUserCommandHandler(
            new FakeIdentityDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)),
            new FakePasswordHasher(),
            repository,
            new FakeIdentityTransactionExecutor());

        var action = async () => await handler.Handle(
            new RegisterUserCommand(" creator@example.com ", "Creator", "supersecret"),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ResourceConflictException>(action);

        Assert.Equal("A user with email ' creator@example.com ' already exists.", exception.Message);
    }
}

public sealed class SeedAdminCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldCreateNewAdminAccount_WhenUserDoesNotExist()
    {
        var repository = new FakeUserRepository();
        var handler = new SeedAdminCommandHandler(
            new FakeIdentityDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)),
            new FakePasswordHasher(),
            repository,
            new FakeIdentityTransactionExecutor());

        var result = await handler.Handle(
            new SeedAdminCommand("admin@example.com", "supersecret", "Admin"),
            CancellationToken.None);

        Assert.True(result.WasNewlyCreated);
        Assert.NotNull(repository.SavedUser);
        Assert.Contains(RoleConstants.Admin, repository.SavedUser!.Roles.Select(x => x.Role));
    }

    [Fact]
    public async Task Handle_ShouldPromoteExistingUser_WhenTheyAreNotAlreadyAdmin()
    {
        var existingUser = User.Register("creator@example.com", "Creator", "hash", DateTime.UtcNow);
        existingUser.AssignRole(RoleConstants.Creator);
        var repository = new FakeUserRepository(existingUser);
        var handler = new SeedAdminCommandHandler(
            new FakeIdentityDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)),
            new FakePasswordHasher(),
            repository,
            new FakeIdentityTransactionExecutor());

        var result = await handler.Handle(
            new SeedAdminCommand("creator@example.com", "ignored", "ignored"),
            CancellationToken.None);

        Assert.False(result.WasNewlyCreated);
        Assert.Equal(existingUser.Id, result.UserId);
        Assert.Contains(RoleConstants.Admin, existingUser.Roles.Select(x => x.Role));
        Assert.True(repository.WasUpdated);
    }

    [Fact]
    public async Task Handle_ShouldBeIdempotent_WhenUserIsAlreadyAdmin()
    {
        var existingUser = User.Register("admin@example.com", "Admin", "hash", DateTime.UtcNow);
        existingUser.AssignRole(RoleConstants.Admin);
        var repository = new FakeUserRepository(existingUser);
        var handler = new SeedAdminCommandHandler(
            new FakeIdentityDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)),
            new FakePasswordHasher(),
            repository,
            new FakeIdentityTransactionExecutor());

        var result = await handler.Handle(
            new SeedAdminCommand("admin@example.com", "ignored", "ignored"),
            CancellationToken.None);

        Assert.False(result.WasNewlyCreated);
        Assert.False(repository.WasUpdated);
    }
}

public sealed class LoginUserCommandHandlerTests
{
    private static LoginUserCommandHandler CreateHandler(
        IAccessTokenProvider? accessTokenProvider = null,
        IPasswordHasher? passwordHasher = null,
        IUserRepository? userRepository = null,
        IRefreshTokenRepository? refreshTokenRepository = null,
        IRefreshTokenService? refreshTokenService = null,
        IIdentityDateTimeProvider? dateTimeProvider = null,
        IIdentityTransactionExecutor? transactionExecutor = null)
    {
        return new LoginUserCommandHandler(
            accessTokenProvider ?? new FakeAccessTokenProvider(),
            passwordHasher ?? new FakePasswordHasher(),
            userRepository ?? new FakeUserRepository(),
            refreshTokenRepository ?? new FakeRefreshTokenRepository(),
            refreshTokenService ?? new FakeRefreshTokenService(),
            dateTimeProvider ?? new FakeIdentityDateTimeProvider(DateTime.UtcNow),
            transactionExecutor ?? new FakeIdentityTransactionExecutor());
    }

    [Fact]
    public async Task Handle_ShouldReturnAccessToken()
    {
        var user = User.Register("creator@example.com", "Creator", "hashed:supersecret", DateTime.UtcNow);
        user.AssignRole(RoleConstants.Creator);
        var repository = new FakeUserRepository(user);
        var tokenProvider = new FakeAccessTokenProvider();
        var refreshTokenRepo = new FakeRefreshTokenRepository();
        var handler = CreateHandler(
            accessTokenProvider: tokenProvider,
            userRepository: repository,
            refreshTokenRepository: refreshTokenRepo);

        var result = await handler.Handle(
            new LoginUserCommand("creator@example.com", "supersecret"),
            CancellationToken.None);

        Assert.Equal("token-value", result.AccessToken);
        Assert.Equal("fake-raw-refresh-token", result.RefreshToken);
        Assert.Single(refreshTokenRepo.Tokens);
        Assert.Contains(PermissionConstants.CampaignsCreate, tokenProvider.LastPermissions);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenPasswordIsInvalid()
    {
        var user = User.Register("creator@example.com", "Creator", "hashed:supersecret", DateTime.UtcNow);
        var handler = CreateHandler(userRepository: new FakeUserRepository(user));

        var action = async () => await handler.Handle(
            new LoginUserCommand("creator@example.com", "wrong-password"),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(action);

        Assert.Equal("Invalid email or password.", exception.Message);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserIsInactive()
    {
        var user = User.Register("creator@example.com", "Creator", "hashed:supersecret", DateTime.UtcNow);
        user.Deactivate();

        var handler = CreateHandler(userRepository: new FakeUserRepository(user));

        var action = async () => await handler.Handle(
            new LoginUserCommand("creator@example.com", "supersecret"),
            CancellationToken.None);

        // Same generic exception as "wrong password"/"no such user" — previously this threw a
        // distinct "account is inactive" message, which confirmed to an attacker that the email
        // belongs to a real (if disabled) account (improvement.md §2.8).
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(action);

        Assert.Equal("Invalid email or password.", exception.Message);
    }

    [Fact]
    public async Task Handle_ShouldVerifyAgainstDummyHash_WhenUserDoesNotExist()
    {
        var hasher = new RecordingPasswordHasher();
        var handler = CreateHandler(passwordHasher: hasher);

        var action = async () => await handler.Handle(
            new LoginUserCommand("nobody@example.com", "whatever"),
            CancellationToken.None);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(action);

        // Constant-time login: even with no matching user, VerifyPassword must still run against
        // DummyHash, so a missing account costs the same PBKDF2 work as a wrong password.
        Assert.Equal(1, hasher.VerifyPasswordCallCount);
        Assert.Equal(hasher.DummyHash, hasher.LastVerifiedHash);
    }
}

public sealed class AssignRoleToUserCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldAddRoleAndEffectivePermissions()
    {
        var user = User.Register("user@example.com", "User", "hash", DateTime.UtcNow);
        var repository = new FakeUserRepository(user);
        var currentUser = new TestCurrentUser
        {
            Permissions = [PermissionConstants.IdentityRolesAssign]
        };
        var handler = new AssignRoleToUserCommandHandler(currentUser, repository, new FakeIdentityTransactionExecutor());

        var result = await handler.Handle(
            new AssignRoleToUserCommand(user.Id, RoleConstants.Moderator),
            CancellationToken.None);

        Assert.Contains(RoleConstants.Moderator, result.Roles);
        Assert.Contains(PermissionConstants.ModerationReview, result.Permissions);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCurrentUserIsNotAuthenticated()
    {
        var user = User.Register("user@example.com", "User", "hash", DateTime.UtcNow);
        var handler = new AssignRoleToUserCommandHandler(
            new TestCurrentUser { IsAuthenticated = false, UserId = Guid.Empty },
            new FakeUserRepository(user),
            new FakeIdentityTransactionExecutor());

        var action = async () => await handler.Handle(
            new AssignRoleToUserCommand(user.Id, RoleConstants.Moderator),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(action);

        Assert.Equal("The current user must be authenticated to assign roles.", exception.Message);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCurrentUserLacksPermission()
    {
        var user = User.Register("user@example.com", "User", "hash", DateTime.UtcNow);
        var handler = new AssignRoleToUserCommandHandler(new TestCurrentUser(), new FakeUserRepository(user), new FakeIdentityTransactionExecutor());

        var action = async () => await handler.Handle(
            new AssignRoleToUserCommand(user.Id, RoleConstants.Moderator),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ForbiddenAccessException>(action);

        Assert.Equal("The current user does not have permission to assign roles.", exception.Message);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserDoesNotExist()
    {
        var userId = Guid.NewGuid();
        var handler = new AssignRoleToUserCommandHandler(
            new TestCurrentUser
            {
                Permissions = [PermissionConstants.IdentityRolesAssign]
            },
            new FakeUserRepository(),
            new FakeIdentityTransactionExecutor());

        var action = async () => await handler.Handle(
            new AssignRoleToUserCommand(userId, RoleConstants.Moderator),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(action);

        Assert.Equal($"User '{userId}' was not found.", exception.Message);
    }
}

public sealed class GrantPermissionToUserCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldAddExplicitPermission()
    {
        var user = User.Register("user@example.com", "User", "hash", DateTime.UtcNow);
        var repository = new FakeUserRepository(user);
        var currentUser = new TestCurrentUser
        {
            Permissions = [PermissionConstants.IdentityPermissionsGrant]
        };
        var handler = new GrantPermissionToUserCommandHandler(currentUser, repository, new FakeIdentityTransactionExecutor());

        var result = await handler.Handle(
            new GrantPermissionToUserCommand(user.Id, PermissionConstants.IdentityPermissionsGrant),
            CancellationToken.None);

        Assert.Contains(PermissionConstants.IdentityPermissionsGrant, result.ExplicitPermissions);
        Assert.Contains(PermissionConstants.IdentityPermissionsGrant, result.Permissions);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCurrentUserIsNotAuthenticated()
    {
        var user = User.Register("user@example.com", "User", "hash", DateTime.UtcNow);
        var handler = new GrantPermissionToUserCommandHandler(
            new TestCurrentUser { IsAuthenticated = false, UserId = Guid.Empty },
            new FakeUserRepository(user),
            new FakeIdentityTransactionExecutor());

        var action = async () => await handler.Handle(
            new GrantPermissionToUserCommand(user.Id, PermissionConstants.IdentityPermissionsGrant),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(action);

        Assert.Equal("The current user must be authenticated to grant permissions.", exception.Message);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCurrentUserLacksPermission()
    {
        var user = User.Register("user@example.com", "User", "hash", DateTime.UtcNow);
        var handler = new GrantPermissionToUserCommandHandler(new TestCurrentUser(), new FakeUserRepository(user), new FakeIdentityTransactionExecutor());

        var action = async () => await handler.Handle(
            new GrantPermissionToUserCommand(user.Id, PermissionConstants.IdentityPermissionsGrant),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ForbiddenAccessException>(action);

        Assert.Equal("The current user does not have permission to grant permissions.", exception.Message);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserDoesNotExist()
    {
        var userId = Guid.NewGuid();
        var handler = new GrantPermissionToUserCommandHandler(
            new TestCurrentUser
            {
                Permissions = [PermissionConstants.IdentityPermissionsGrant]
            },
            new FakeUserRepository(),
            new FakeIdentityTransactionExecutor());

        var action = async () => await handler.Handle(
            new GrantPermissionToUserCommand(userId, PermissionConstants.IdentityPermissionsGrant),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(action);

        Assert.Equal($"User '{userId}' was not found.", exception.Message);
    }
}

public sealed class GetCurrentUserQueryHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnPersistedCurrentUser()
    {
        var user = User.Register("user@example.com", "User", "hash", DateTime.UtcNow);
        user.AssignRole(RoleConstants.Creator);
        var repository = new FakeUserRepository(user);
        var handler = new GetCurrentUserQueryHandler(
            new TestCurrentUser { UserId = user.Id },
            repository);

        var result = await handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        Assert.Equal(user.Id, result.UserId);
        Assert.Contains(RoleConstants.Creator, result.Roles);
        Assert.Contains(PermissionConstants.CampaignsCreate, result.Permissions);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCurrentUserIsNotAuthenticated()
    {
        var handler = new GetCurrentUserQueryHandler(
            new TestCurrentUser { IsAuthenticated = false, UserId = Guid.Empty },
            new FakeUserRepository());

        var action = async () => await handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(action);

        Assert.Equal("The current user is not authenticated.", exception.Message);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenPersistedUserIsMissing()
    {
        var userId = Guid.NewGuid();
        var handler = new GetCurrentUserQueryHandler(
            new TestCurrentUser { UserId = userId },
            new FakeUserRepository());

        var action = async () => await handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(action);

        Assert.Equal($"User '{userId}' was not found.", exception.Message);
    }
}

public sealed class RegisterUserCommandValidatorTests
{
    [Fact]
    public void Validate_ShouldReturnErrors_WhenCommandIsInvalid()
    {
        var validator = new RegisterUserCommandValidator();
        var result = validator.Validate(new RegisterUserCommand("", "", "short"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RegisterUserCommand.Email));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RegisterUserCommand.DisplayName));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RegisterUserCommand.Password));
    }
}

public sealed class LoginUserCommandValidatorTests
{
    [Fact]
    public void Validate_ShouldReturnErrors_WhenCommandIsInvalid()
    {
        var validator = new LoginUserCommandValidator();
        var result = validator.Validate(new LoginUserCommand("", ""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(LoginUserCommand.Email));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(LoginUserCommand.Password));
    }
}

public sealed class AssignRoleToUserCommandValidatorTests
{
    [Fact]
    public void Validate_ShouldReturnErrors_WhenCommandIsInvalid()
    {
        var validator = new AssignRoleToUserCommandValidator();
        var result = validator.Validate(new AssignRoleToUserCommand(Guid.Empty, "NotARole"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AssignRoleToUserCommand.UserId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AssignRoleToUserCommand.Role));
    }
}

public sealed class GrantPermissionToUserCommandValidatorTests
{
    [Fact]
    public void Validate_ShouldReturnErrors_WhenCommandIsInvalid()
    {
        var validator = new GrantPermissionToUserCommandValidator();
        var result = validator.Validate(new GrantPermissionToUserCommand(Guid.Empty, "NotAPermission"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(GrantPermissionToUserCommand.UserId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(GrantPermissionToUserCommand.Permission));
    }
}

internal sealed class FakeUserRepository : IUserRepository
{
    private readonly List<User> _users;

    public FakeUserRepository(params User[] users)
    {
        _users = users.ToList();
    }

    public User? SavedUser { get; private set; }

    public Task AddAsync(User user, CancellationToken cancellationToken)
    {
        _users.Add(user);
        SavedUser = user;
        return Task.CompletedTask;
    }

    public Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return Task.FromResult(_users.SingleOrDefault(x => x.Id == userId));
    }

    public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        return Task.FromResult(_users.SingleOrDefault(x => x.NormalizedEmail == normalizedEmail));
    }

    public Task<bool> AnyAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(_users.Count > 0);
    }

    public bool WasUpdated { get; private set; }

    public Task UpdateAsync(User user, CancellationToken cancellationToken)
    {
        WasUpdated = true;
        SavedUser = user;
        return Task.CompletedTask;
    }
}

internal sealed class FakeIdentityDateTimeProvider : IIdentityDateTimeProvider
{
    public FakeIdentityDateTimeProvider(DateTime utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTime UtcNow { get; }
}

internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string DummyHash { get; } = "hashed:__dummy__";

    public string HashPassword(string password)
    {
        return $"hashed:{password}";
    }

    public bool VerifyPassword(string passwordHash, string password)
    {
        return passwordHash == $"hashed:{password}";
    }
}

internal sealed class RecordingPasswordHasher : IPasswordHasher
{
    public string DummyHash { get; } = "dummy-hash";
    public int VerifyPasswordCallCount { get; private set; }
    public string? LastVerifiedHash { get; private set; }

    public string HashPassword(string password) => $"hashed:{password}";

    public bool VerifyPassword(string passwordHash, string password)
    {
        VerifyPasswordCallCount++;
        LastVerifiedHash = passwordHash;
        return false;
    }
}

internal sealed class FakeAccessTokenProvider : IAccessTokenProvider
{
    public IReadOnlyCollection<string> LastPermissions { get; private set; } = [];

    public AccessToken Create(User user, IReadOnlyCollection<string> permissions)
    {
        LastPermissions = permissions;
        return new AccessToken("token-value", new DateTime(2026, 4, 6, 13, 0, 0, DateTimeKind.Utc));
    }
}

internal sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly List<RefreshToken> _tokens = [];

    public IReadOnlyList<RefreshToken> Tokens => _tokens;

    public Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
    {
        _tokens.Add(refreshToken);
        return Task.CompletedTask;
    }

    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        return Task.FromResult(_tokens.SingleOrDefault(x => x.TokenHash == tokenHash));
    }

    public Task<List<RefreshToken>> GetActiveByUserIdAsync(Guid userId, DateTime now, CancellationToken cancellationToken)
    {
        return Task.FromResult(_tokens.Where(x => x.UserId == userId && x.IsActive(now)).ToList());
    }

    public Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

internal sealed class FakeRefreshTokenService : IRefreshTokenService
{
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromDays(7);
    public string NextToken { get; set; } = "fake-raw-refresh-token";

    public string GenerateToken() => NextToken;

    public string Hash(string rawToken) => $"hashed:{rawToken}";
}

internal sealed class FakeSecurityStampRevocationStore : ISecurityStampRevocationStore
{
    private readonly HashSet<(Guid UserId, Guid Stamp)> _revoked = [];

    public List<(Guid UserId, Guid Stamp)> RevokedCalls { get; } = [];

    public Task RevokeAsync(Guid userId, Guid revokedStamp, CancellationToken cancellationToken)
    {
        _revoked.Add((userId, revokedStamp));
        RevokedCalls.Add((userId, revokedStamp));
        return Task.CompletedTask;
    }

    public Task<bool> IsRevokedAsync(Guid userId, Guid stamp, CancellationToken cancellationToken)
        => Task.FromResult(_revoked.Contains((userId, stamp)));
}

public sealed class RefreshTokenDomainTests
{
    [Fact]
    public void Issue_ShouldSetExpirationRelativeToIssuedAtUtc()
    {
        var issuedAt = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);

        var token = RefreshToken.Issue(Guid.NewGuid(), "hash", issuedAt, TimeSpan.FromDays(30));

        Assert.Equal(issuedAt, token.IssuedAtUtc);
        Assert.Equal(issuedAt.AddDays(30), token.ExpiresAtUtc);
        Assert.False(token.IsRevoked);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Issue_ShouldThrow_WhenTokenHashIsMissing(string blankHash)
    {
        var action = () => RefreshToken.Issue(Guid.NewGuid(), blankHash, DateTime.UtcNow, TimeSpan.FromDays(1));

        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Issue_ShouldThrow_WhenLifetimeIsNotPositive()
    {
        var action = () => RefreshToken.Issue(Guid.NewGuid(), "hash", DateTime.UtcNow, TimeSpan.Zero);

        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void IsActive_ShouldReturnFalse_OnceExpired()
    {
        var issuedAt = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);
        var token = RefreshToken.Issue(Guid.NewGuid(), "hash", issuedAt, TimeSpan.FromMinutes(1));

        Assert.True(token.IsActive(issuedAt.AddSeconds(30)));
        Assert.False(token.IsActive(issuedAt.AddMinutes(2)));
    }

    [Fact]
    public void Revoke_ShouldSetRevokedAtAndReplacedByHash()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "hash", DateTime.UtcNow, TimeSpan.FromDays(1));
        var now = DateTime.UtcNow;

        token.Revoke(now, "new-hash");

        Assert.True(token.IsRevoked);
        Assert.Equal(now, token.RevokedAtUtc);
        Assert.Equal("new-hash", token.ReplacedByTokenHash);
        Assert.False(token.IsActive(now));
    }

    [Fact]
    public void Revoke_ShouldBeIdempotent_AndKeepTheFirstRevocationDetails()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "hash", DateTime.UtcNow, TimeSpan.FromDays(1));
        var firstRevocation = DateTime.UtcNow;

        token.Revoke(firstRevocation, "first-replacement");
        token.Revoke(firstRevocation.AddMinutes(5), "second-replacement");

        Assert.Equal(firstRevocation, token.RevokedAtUtc);
        Assert.Equal("first-replacement", token.ReplacedByTokenHash);
    }
}

public sealed class RefreshAccessTokenCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);

    private static RefreshAccessTokenCommandHandler CreateHandler(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ISecurityStampRevocationStore? revocationStore = null,
        IAccessTokenProvider? accessTokenProvider = null,
        IRefreshTokenService? refreshTokenService = null)
    {
        return new RefreshAccessTokenCommandHandler(
            accessTokenProvider ?? new FakeAccessTokenProvider(),
            userRepository,
            refreshTokenRepository,
            refreshTokenService ?? new FakeRefreshTokenService(),
            revocationStore ?? new FakeSecurityStampRevocationStore(),
            new FakeIdentityDateTimeProvider(Now),
            new FakeIdentityTransactionExecutor());
    }

    [Fact]
    public async Task Handle_ShouldRotateToken_AndReturnNewAccessAndRefreshTokens()
    {
        var user = User.Register("creator@example.com", "Creator", "hash", DateTime.UtcNow);
        var userRepository = new FakeUserRepository(user);
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var refreshTokenService = new FakeRefreshTokenService { NextToken = "rotated-token" };
        var oldToken = RefreshToken.Issue(user.Id, refreshTokenService.Hash("raw-old-token"), Now.AddMinutes(-1), TimeSpan.FromDays(30));
        await refreshTokenRepository.AddAsync(oldToken, CancellationToken.None);
        var handler = CreateHandler(userRepository, refreshTokenRepository, refreshTokenService: refreshTokenService);

        var result = await handler.Handle(new RefreshAccessTokenCommand("raw-old-token"), CancellationToken.None);

        Assert.Equal("rotated-token", result.RefreshToken);
        Assert.True(oldToken.IsRevoked);
        Assert.Equal(refreshTokenService.Hash("rotated-token"), oldToken.ReplacedByTokenHash);
        Assert.Equal(2, refreshTokenRepository.Tokens.Count);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTokenIsUnknown()
    {
        var handler = CreateHandler(new FakeUserRepository(), new FakeRefreshTokenRepository());

        var action = async () => await handler.Handle(new RefreshAccessTokenCommand("never-issued"), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(action);
        Assert.Equal("Invalid refresh token.", exception.Message);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTokenHasExpired()
    {
        var user = User.Register("creator@example.com", "Creator", "hash", DateTime.UtcNow);
        var userRepository = new FakeUserRepository(user);
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var refreshTokenService = new FakeRefreshTokenService();
        var expiredToken = RefreshToken.Issue(user.Id, refreshTokenService.Hash("raw-token"), Now.AddDays(-31), TimeSpan.FromDays(30));
        await refreshTokenRepository.AddAsync(expiredToken, CancellationToken.None);
        var handler = CreateHandler(userRepository, refreshTokenRepository, refreshTokenService: refreshTokenService);

        var action = async () => await handler.Handle(new RefreshAccessTokenCommand("raw-token"), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(action);
        Assert.Equal("Refresh token has expired.", exception.Message);
    }

    [Fact]
    public async Task Handle_ShouldDetectReuse_AndRevokeEverySessionForTheUser()
    {
        // The defining scenario TICKET-036 exists for: an already-rotated-away token being
        // presented a second time. Two active sessions exist for the user; presenting the
        // already-revoked token must kill both, rotate the security stamp, and blacklist the
        // stamp that was valid a moment ago.
        var user = User.Register("creator@example.com", "Creator", "hash", DateTime.UtcNow);
        var compromisedStamp = user.SecurityStamp;
        var userRepository = new FakeUserRepository(user);
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var refreshTokenService = new FakeRefreshTokenService();
        var revocationStore = new FakeSecurityStampRevocationStore();

        var alreadyRotatedToken = RefreshToken.Issue(user.Id, refreshTokenService.Hash("stolen-token"), Now.AddDays(-1), TimeSpan.FromDays(30));
        alreadyRotatedToken.Revoke(Now.AddMinutes(-30), "some-other-hash");
        await refreshTokenRepository.AddAsync(alreadyRotatedToken, CancellationToken.None);

        var otherActiveToken = RefreshToken.Issue(user.Id, refreshTokenService.Hash("other-active-token"), Now.AddMinutes(-10), TimeSpan.FromDays(30));
        await refreshTokenRepository.AddAsync(otherActiveToken, CancellationToken.None);

        var handler = CreateHandler(userRepository, refreshTokenRepository, revocationStore, refreshTokenService: refreshTokenService);

        var action = async () => await handler.Handle(new RefreshAccessTokenCommand("stolen-token"), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(action);
        Assert.Equal("Refresh token reuse detected; all sessions have been revoked.", exception.Message);
        Assert.True(otherActiveToken.IsRevoked);
        Assert.NotEqual(compromisedStamp, user.SecurityStamp);
        Assert.Contains((user.Id, compromisedStamp), revocationStore.RevokedCalls);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserIsInactive()
    {
        var user = User.Register("creator@example.com", "Creator", "hash", DateTime.UtcNow);
        user.Deactivate();
        var userRepository = new FakeUserRepository(user);
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var refreshTokenService = new FakeRefreshTokenService();
        var token = RefreshToken.Issue(user.Id, refreshTokenService.Hash("raw-token"), Now.AddMinutes(-1), TimeSpan.FromDays(30));
        await refreshTokenRepository.AddAsync(token, CancellationToken.None);
        var handler = CreateHandler(userRepository, refreshTokenRepository, refreshTokenService: refreshTokenService);

        var action = async () => await handler.Handle(new RefreshAccessTokenCommand("raw-token"), CancellationToken.None);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(action);
    }
}

public sealed class LogoutCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldRevokeToken_AndRotateSecurityStamp_AndBlacklistOldStamp()
    {
        var user = User.Register("creator@example.com", "Creator", "hash", DateTime.UtcNow);
        var staleStamp = user.SecurityStamp;
        var userRepository = new FakeUserRepository(user);
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var refreshTokenService = new FakeRefreshTokenService();
        var revocationStore = new FakeSecurityStampRevocationStore();
        var token = RefreshToken.Issue(user.Id, refreshTokenService.Hash("raw-token"), DateTime.UtcNow, TimeSpan.FromDays(30));
        await refreshTokenRepository.AddAsync(token, CancellationToken.None);
        var handler = new LogoutCommandHandler(
            new TestCurrentUser { UserId = user.Id },
            userRepository,
            refreshTokenRepository,
            refreshTokenService,
            revocationStore,
            new FakeIdentityDateTimeProvider(DateTime.UtcNow),
            new FakeIdentityTransactionExecutor());

        await handler.Handle(new LogoutCommand("raw-token"), CancellationToken.None);

        Assert.True(token.IsRevoked);
        Assert.NotEqual(staleStamp, user.SecurityStamp);
        Assert.Contains((user.Id, staleStamp), revocationStore.RevokedCalls);
    }

    [Fact]
    public async Task Handle_ShouldIgnoreATokenBelongingToAnotherUser_ButStillLogOutTheCaller()
    {
        var caller = User.Register("caller@example.com", "Caller", "hash", DateTime.UtcNow);
        var someoneElse = User.Register("someone-else@example.com", "Someone Else", "hash", DateTime.UtcNow);
        var userRepository = new FakeUserRepository(caller, someoneElse);
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var refreshTokenService = new FakeRefreshTokenService();
        var someoneElsesToken = RefreshToken.Issue(someoneElse.Id, refreshTokenService.Hash("not-mine"), DateTime.UtcNow, TimeSpan.FromDays(30));
        await refreshTokenRepository.AddAsync(someoneElsesToken, CancellationToken.None);
        var callerStamp = caller.SecurityStamp;
        var handler = new LogoutCommandHandler(
            new TestCurrentUser { UserId = caller.Id },
            userRepository,
            refreshTokenRepository,
            refreshTokenService,
            new FakeSecurityStampRevocationStore(),
            new FakeIdentityDateTimeProvider(DateTime.UtcNow),
            new FakeIdentityTransactionExecutor());

        await handler.Handle(new LogoutCommand("not-mine"), CancellationToken.None);

        Assert.False(someoneElsesToken.IsRevoked);
        Assert.NotEqual(callerStamp, caller.SecurityStamp);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCallerIsNotAuthenticated()
    {
        var handler = new LogoutCommandHandler(
            new TestCurrentUser { IsAuthenticated = false, UserId = Guid.Empty },
            new FakeUserRepository(),
            new FakeRefreshTokenRepository(),
            new FakeRefreshTokenService(),
            new FakeSecurityStampRevocationStore(),
            new FakeIdentityDateTimeProvider(DateTime.UtcNow),
            new FakeIdentityTransactionExecutor());

        var action = async () => await handler.Handle(new LogoutCommand("whatever"), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(action);
        Assert.Equal("The current user must be authenticated to log out.", exception.Message);
    }
}

public sealed class RefreshAccessTokenCommandValidatorTests
{
    [Fact]
    public void Validate_ShouldReturnError_WhenRefreshTokenIsMissing()
    {
        var validator = new RefreshAccessTokenCommandValidator();

        var result = validator.Validate(new RefreshAccessTokenCommand(""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RefreshAccessTokenCommand.RefreshToken));
    }
}

public sealed class LogoutCommandValidatorTests
{
    [Fact]
    public void Validate_ShouldReturnError_WhenRefreshTokenIsMissing()
    {
        var validator = new LogoutCommandValidator();

        var result = validator.Validate(new LogoutCommand(""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(LogoutCommand.RefreshToken));
    }
}

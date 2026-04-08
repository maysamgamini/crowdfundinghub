using CrowdFunding.Modules.Identity.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Identity.Application.Abstractions.Services;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.AssignRoleToUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.GrantPermissionToUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.LoginUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.RegisterUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Queries.GetCurrentUser;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using CrowdFunding.Modules.Identity.Domain.Aggregates;

namespace CrowdFunding.UnitTests;

public sealed class RegisterUserCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldAssignAdminRoleToFirstUser()
    {
        var repository = new FakeUserRepository();
        var handler = new RegisterUserCommandHandler(
            new FakeIdentityDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)),
            new FakePasswordHasher(),
            repository);

        var result = await handler.Handle(
            new RegisterUserCommand("admin@example.com", "Admin", "supersecret"),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.UserId);
        Assert.NotNull(repository.SavedUser);
        Assert.Contains(RoleConstants.Admin, repository.SavedUser!.Roles.Select(x => x.Role));
    }

    [Fact]
    public async Task Handle_ShouldAssignCreatorAndBackerRolesToLaterUsers()
    {
        var existingUser = User.Register("existing@example.com", "Existing", "hash", DateTime.UtcNow);
        var repository = new FakeUserRepository(existingUser);
        var handler = new RegisterUserCommandHandler(
            new FakeIdentityDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)),
            new FakePasswordHasher(),
            repository);

        await handler.Handle(
            new RegisterUserCommand("creator@example.com", "Creator", "supersecret"),
            CancellationToken.None);

        Assert.NotNull(repository.SavedUser);
        Assert.Contains(RoleConstants.Creator, repository.SavedUser!.Roles.Select(x => x.Role));
        Assert.Contains(RoleConstants.Backer, repository.SavedUser.Roles.Select(x => x.Role));
    }
}

public sealed class LoginUserCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnAccessToken()
    {
        var user = User.Register("creator@example.com", "Creator", "hashed:supersecret", DateTime.UtcNow);
        user.AssignRole(RoleConstants.Creator);
        var repository = new FakeUserRepository(user);
        var tokenProvider = new FakeAccessTokenProvider();
        var handler = new LoginUserCommandHandler(tokenProvider, new FakePasswordHasher(), repository);

        var result = await handler.Handle(
            new LoginUserCommand("creator@example.com", "supersecret"),
            CancellationToken.None);

        Assert.Equal("token-value", result.AccessToken);
        Assert.Contains(PermissionConstants.CampaignsCreate, tokenProvider.LastPermissions);
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
        var handler = new AssignRoleToUserCommandHandler(currentUser, repository);

        var result = await handler.Handle(
            new AssignRoleToUserCommand(user.Id, RoleConstants.Moderator),
            CancellationToken.None);

        Assert.Contains(RoleConstants.Moderator, result.Roles);
        Assert.Contains(PermissionConstants.ModerationReview, result.Permissions);
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
        var handler = new GrantPermissionToUserCommandHandler(currentUser, repository);

        var result = await handler.Handle(
            new GrantPermissionToUserCommand(user.Id, PermissionConstants.IdentityPermissionsGrant),
            CancellationToken.None);

        Assert.Contains(PermissionConstants.IdentityPermissionsGrant, result.ExplicitPermissions);
        Assert.Contains(PermissionConstants.IdentityPermissionsGrant, result.Permissions);
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

    public Task UpdateAsync(User user, CancellationToken cancellationToken)
    {
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
    public string HashPassword(string password)
    {
        return $"hashed:{password}";
    }

    public bool VerifyPassword(string passwordHash, string password)
    {
        return passwordHash == $"hashed:{password}";
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

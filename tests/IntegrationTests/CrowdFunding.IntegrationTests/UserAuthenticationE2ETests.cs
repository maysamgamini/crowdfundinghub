using System.Net;
using System.Net.Http.Json;
using CrowdFunding.API.Contracts.Identity;
using CrowdFunding.API.Migrations;
using CrowdFunding.Modules.Identity.Contracts.Authorization;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// Exercises the full HTTP registration/login/authorization pipeline — routing, model binding,
/// FluentValidation, ES256 JWT issuance and verification, and claims-based authorization policies —
/// none of which the module-internal concurrency tests touch.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class UserAuthenticationE2ETests
{
    private readonly CrowdFundingApiFactory _factory;

    public UserAuthenticationE2ETests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_ThenLogin_ThenMe_ShouldReturnDefaultCreatorAndBackerPermissions()
    {
        using var client = _factory.CreateClient();
        var email = ApiTestExtensions.UniqueEmail("register-flow");

        var (userId, accessToken) = await client.RegisterAndLoginAsync(email, "Register Flow User");
        client.SetBearerToken(accessToken);

        var response = await client.GetAsync("/api/identity/me");
        response.EnsureSuccessStatusCode();
        var me = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();

        Assert.NotNull(me);
        Assert.Equal(userId, me!.UserId);
        Assert.Contains(RoleConstants.Creator, me.Roles);
        Assert.Contains(RoleConstants.Backer, me.Roles);
        Assert.Contains(PermissionConstants.CampaignsCreate, me.Permissions);
        Assert.Contains(PermissionConstants.CampaignsContribute, me.Permissions);
        Assert.DoesNotContain(PermissionConstants.ModerationReview, me.Permissions);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();
        var email = ApiTestExtensions.UniqueEmail("bad-login");
        await client.RegisterUserAsync(email, "Bad Login User");

        var response = await client.PostAsJsonAsync(
            "/api/identity/login", new LoginUserRequest(email, "not-the-right-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ShouldReturnConflict()
    {
        using var client = _factory.CreateClient();
        var email = ApiTestExtensions.UniqueEmail("duplicate");
        await client.RegisterUserAsync(email, "First User");

        var response = await client.PostAsJsonAsync(
            "/api/identity/register", new RegisterUserRequest(email, "Second User", ApiTestExtensions.DefaultPassword));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AssignRole_ByAdmin_ShouldGrantModerationPermission()
    {
        using var client = _factory.CreateClient();

        var adminEmail = ApiTestExtensions.UniqueEmail("admin-assign");
        await AdminSeeder.RunAsync(_factory.Services, adminEmail, ApiTestExtensions.DefaultPassword, "Admin Assign");
        var adminToken = await client.LoginUserAsync(adminEmail);

        var targetEmail = ApiTestExtensions.UniqueEmail("promotable");
        var (targetUserId, _) = await client.RegisterAndLoginAsync(targetEmail, "Promotable User");

        client.SetBearerToken(adminToken);
        var response = await client.PostAsJsonAsync(
            $"/api/identity/users/{targetUserId}/roles", new AssignRoleToUserRequest(RoleConstants.Moderator));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<AssignRoleToUserResponse>();
        Assert.NotNull(body);
        Assert.Contains(RoleConstants.Moderator, body!.Roles);
        Assert.Contains(PermissionConstants.ModerationReview, body.Permissions);
    }

    [Fact]
    public async Task AssignRole_WithoutPermission_ShouldReturnForbidden()
    {
        using var client = _factory.CreateClient();

        var callerEmail = ApiTestExtensions.UniqueEmail("no-permission-caller");
        var (_, callerToken) = await client.RegisterAndLoginAsync(callerEmail, "No Permission Caller");

        var targetEmail = ApiTestExtensions.UniqueEmail("no-permission-target");
        var (targetUserId, _) = await client.RegisterAndLoginAsync(targetEmail, "No Permission Target");

        client.SetBearerToken(callerToken);
        var response = await client.PostAsJsonAsync(
            $"/api/identity/users/{targetUserId}/roles", new AssignRoleToUserRequest(RoleConstants.Moderator));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutBearerToken_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/identity/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

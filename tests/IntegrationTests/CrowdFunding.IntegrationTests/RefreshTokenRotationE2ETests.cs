using System.Net;
using System.Net.Http.Json;
using CrowdFunding.API.Contracts.Identity;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// TICKET-036: exercises Refresh Token Rotation, breach reuse detection, and logout against the
/// full HTTP pipeline — including the JwtBearerEvents.OnTokenValidated Redis check in Program.cs,
/// which the module-internal unit tests (RefreshAccessTokenCommandHandlerTests,
/// LogoutCommandHandlerTests) cannot exercise since they never go through real token validation.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class RefreshTokenRotationE2ETests
{
    private readonly CrowdFundingApiFactory _factory;

    public RefreshTokenRotationE2ETests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Refresh_ShouldIssueNewTokenPair_AndRevokeThePresentedRefreshToken()
    {
        using var client = _factory.CreateClient();
        var email = ApiTestExtensions.UniqueEmail("refresh-rotation");
        await client.RegisterUserAsync(email);
        var login = await client.LoginWithTokensAsync(email);

        var refreshResponse = await client.PostAsJsonAsync(
            "/api/identity/refresh", new RefreshAccessTokenRequest(login.RefreshToken));
        refreshResponse.EnsureSuccessStatusCode();
        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<RefreshAccessTokenResponse>();

        Assert.NotNull(refreshed);
        Assert.NotEqual(login.AccessToken, refreshed!.AccessToken);
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);

        // The new access token works immediately.
        client.SetBearerToken(refreshed.AccessToken);
        var meResponse = await client.GetAsync("/api/identity/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        // The original refresh token was consumed by rotation and cannot be used again.
        var secondAttempt = await client.PostAsJsonAsync(
            "/api/identity/refresh", new RefreshAccessTokenRequest(login.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, secondAttempt.StatusCode);
    }

    [Fact]
    public async Task ReusingAnAlreadyRotatedRefreshToken_ShouldRevokeEverySessionForTheAccount()
    {
        using var loginClientA = _factory.CreateClient();
        using var loginClientB = _factory.CreateClient();
        using var attackerClient = _factory.CreateClient();
        var email = ApiTestExtensions.UniqueEmail("reuse-detection");
        await loginClientA.RegisterUserAsync(email);

        // Two independent sessions for the same account (e.g. two devices), both minted while
        // the account's security stamp is still the original one.
        var sessionA = await loginClientA.LoginWithTokensAsync(email);
        var sessionB = await loginClientB.LoginWithTokensAsync(email);

        // Session A rotates once, normally.
        var rotateResponse = await loginClientA.PostAsJsonAsync(
            "/api/identity/refresh", new RefreshAccessTokenRequest(sessionA.RefreshToken));
        rotateResponse.EnsureSuccessStatusCode();

        // Someone presents Session A's now-superseded refresh token again — the reuse signal.
        var reuseResponse = await attackerClient.PostAsJsonAsync(
            "/api/identity/refresh", new RefreshAccessTokenRequest(sessionA.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);

        // Session B's access token was minted before the breach response rotated the account's
        // security stamp, but is still within its cryptographic lifetime. It must now be
        // rejected too — proving revocation propagated to every session, not just the one whose
        // token was replayed.
        loginClientB.SetBearerToken(sessionB.AccessToken);
        var sessionBMeResponse = await loginClientB.GetAsync("/api/identity/me");
        Assert.Equal(HttpStatusCode.Unauthorized, sessionBMeResponse.StatusCode);

        // Session B's own refresh token is also revoked as part of the same breach response.
        var sessionBRefreshResponse = await loginClientB.PostAsJsonAsync(
            "/api/identity/refresh", new RefreshAccessTokenRequest(sessionB.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, sessionBRefreshResponse.StatusCode);
    }

    [Fact]
    public async Task Logout_ShouldInstantlyInvalidateTheStillUnexpiredAccessToken()
    {
        using var client = _factory.CreateClient();
        var email = ApiTestExtensions.UniqueEmail("logout-revocation");
        await client.RegisterUserAsync(email);
        var login = await client.LoginWithTokensAsync(email);
        client.SetBearerToken(login.AccessToken);

        var beforeLogout = await client.GetAsync("/api/identity/me");
        Assert.Equal(HttpStatusCode.OK, beforeLogout.StatusCode);

        var logoutResponse = await client.PostAsJsonAsync("/api/identity/logout", new LogoutRequest(login.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        // Same access token, same signature, still inside its cryptographic lifetime — yet
        // rejected, because the security stamp it carries was blacklisted the instant logout ran.
        var afterLogout = await client.GetAsync("/api/identity/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);

        // The refresh token was revoked too.
        var refreshAfterLogout = await client.PostAsJsonAsync(
            "/api/identity/refresh", new RefreshAccessTokenRequest(login.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogout.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithAnUnknownToken_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/identity/refresh", new RefreshAccessTokenRequest("this-token-was-never-issued"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutBearerToken_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/identity/logout", new LogoutRequest("whatever"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

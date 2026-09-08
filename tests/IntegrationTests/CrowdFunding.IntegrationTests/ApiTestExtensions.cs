using System.Net.Http.Headers;
using System.Net.Http.Json;
using CrowdFunding.API.Contracts.Identity;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// Small HTTP helpers shared by the E2E test suites — real register/login round-trips against
/// the running pipeline rather than hand-crafted JWTs, so authentication/authorization are
/// actually exercised end-to-end.
/// </summary>
internal static class ApiTestExtensions
{
    public const string DefaultPassword = "SuperSecret123!";

    public static async Task<Guid> RegisterUserAsync(
        this HttpClient client, string email, string displayName = "Test User", string password = DefaultPassword)
    {
        var response = await client.PostAsJsonAsync(
            "/api/identity/register", new RegisterUserRequest(email, displayName, password));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<RegisterUserResponse>();
        return body!.UserId;
    }

    public static async Task<string> LoginUserAsync(
        this HttpClient client, string email, string password = DefaultPassword)
    {
        var response = await client.PostAsJsonAsync(
            "/api/identity/login", new LoginUserRequest(email, password));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginUserResponse>();
        return body!.AccessToken;
    }

    public static async Task<(Guid UserId, string AccessToken)> RegisterAndLoginAsync(
        this HttpClient client, string email, string displayName = "Test User", string password = DefaultPassword)
    {
        var userId = await client.RegisterUserAsync(email, displayName, password);
        var accessToken = await client.LoginUserAsync(email, password);
        return (userId, accessToken);
    }

    public static void SetBearerToken(this HttpClient client, string accessToken)
        => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    public static string UniqueEmail(string label) => $"{label}.{Guid.NewGuid():N}@example.com";
}

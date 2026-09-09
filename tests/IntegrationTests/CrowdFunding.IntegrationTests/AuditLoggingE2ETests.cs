using System.Net.Http.Json;
using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.API.Contracts.Identity;
using CrowdFunding.API.Migrations;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// TICKET-039: proves the audit-logging pipeline behavior actually intercepts real commands
/// dispatched through the full HTTP pipeline and writes forensic records to
/// <c>system.audit_records</c> — without <c>CancelCampaignCommandHandler</c> or
/// <c>AssignRoleToUserCommandHandler</c> containing any audit-specific code themselves.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class AuditLoggingE2ETests
{
    private readonly CrowdFundingApiFactory _factory;

    public AuditLoggingE2ETests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CancellingACampaign_ShouldWriteAnAuditRecord_EvenWhenTheCallerIsNotAnAdmin()
    {
        using var client = _factory.CreateClient();
        var email = ApiTestExtensions.UniqueEmail("audit-cancel-owner");
        var (ownerId, ownerToken) = await client.RegisterAndLoginAsync(email);
        client.SetBearerToken(ownerToken);

        var createResponse = await client.PostAsJsonAsync("/api/campaigns", new CreateCampaignRequest(
            "Audit Logging Test Campaign",
            "A story that is definitely longer than twenty characters to satisfy validation rules.",
            "Technology",
            10_000m,
            "USD",
            DateTime.UtcNow.AddDays(30)));
        createResponse.EnsureSuccessStatusCode();
        var campaignId = (await createResponse.Content.ReadFromJsonAsync<CreateCampaignResponse>())!.CampaignId;

        var cancelResponse = await client.PostAsync($"/api/campaigns/{campaignId}/cancel", content: null);
        cancelResponse.EnsureSuccessStatusCode();

        await using var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT actor_id, actor_email, payload_json, ip_address, user_agent " +
            "FROM system.audit_records WHERE action = 'Campaign.Cancel' AND payload_json LIKE @campaignId " +
            "ORDER BY timestamp_utc DESC LIMIT 1";
        command.Parameters.AddWithValue("campaignId", $"%{campaignId}%");

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "Expected an audit record for the Campaign.Cancel action.");

        Assert.Equal(ownerId, reader.GetGuid(0));
        Assert.Equal(email, reader.GetString(1));
        Assert.Contains(campaignId.ToString(), reader.GetString(2));
        Assert.False(string.IsNullOrWhiteSpace(reader.GetString(3)));
        Assert.False(string.IsNullOrWhiteSpace(reader.GetString(4)));
    }

    [Fact]
    public async Task AnyCommandExecutedByAnAdmin_ShouldWriteAnAuditRecord_EvenWithoutTheAttribute()
    {
        using var client = _factory.CreateClient();
        var adminEmail = ApiTestExtensions.UniqueEmail("audit-admin");
        await AdminSeeder.RunAsync(_factory.Services, adminEmail, ApiTestExtensions.DefaultPassword, "Audit Admin");
        var adminToken = await client.LoginUserAsync(adminEmail);

        var targetEmail = ApiTestExtensions.UniqueEmail("audit-admin-target");
        var (targetUserId, _) = await client.RegisterAndLoginAsync(targetEmail, "Audit Admin Target");

        client.SetBearerToken(adminToken);
        var response = await client.PostAsJsonAsync(
            $"/api/identity/users/{targetUserId}/roles", new AssignRoleToUserRequest(RoleConstants.Moderator));
        response.EnsureSuccessStatusCode();

        await using var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT action FROM system.audit_records WHERE action = 'AssignRoleToUserCommand' " +
            "AND payload_json LIKE @targetUserId ORDER BY timestamp_utc DESC LIMIT 1";
        command.Parameters.AddWithValue("targetUserId", $"%{targetUserId}%");

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "Expected an audit record for an Admin-executed command with no [AuditableAction] attribute.");
    }

    [Fact]
    public async Task AuditRecordsTable_ShouldRejectUpdatesAndDeletes_FromARoleGrantedOnlySelectAndInsert()
    {
        const string restrictedRole = "audit_append_only_test_role";
        const string restrictedPassword = "AuditAppendOnly123!";

        await using var adminConnection = await OpenConnectionAsync();

        await ExecuteNonQueryAsync(adminConnection, $"DROP ROLE IF EXISTS {restrictedRole}");
        await ExecuteNonQueryAsync(adminConnection, $"CREATE ROLE {restrictedRole} LOGIN PASSWORD '{restrictedPassword}'");
        await ExecuteNonQueryAsync(adminConnection, $"GRANT USAGE ON SCHEMA system TO {restrictedRole}");
        await ExecuteNonQueryAsync(adminConnection, $"GRANT SELECT, INSERT ON system.audit_records TO {restrictedRole}");

        // A row for the restricted-role connection to attempt to mutate — inserted via the admin
        // connection so this test doesn't depend on execution order relative to the others.
        var recordId = Guid.NewGuid();
        await ExecuteNonQueryAsync(
            adminConnection,
            "INSERT INTO system.audit_records " +
            "(id, actor_id, actor_email, action, command_type, payload_json, ip_address, user_agent, timestamp_utc) " +
            "VALUES (@id, @actorId, 'seed@example.com', 'Seed.Action', 'SeedCommand', '{}', '127.0.0.1', 'seed-agent', now())",
            command =>
            {
                command.Parameters.AddWithValue("id", recordId);
                command.Parameters.AddWithValue("actorId", Guid.NewGuid());
            });

        var restrictedConnectionString = BuildConnectionStringForRole(restrictedRole, restrictedPassword);
        await using var restrictedConnection = new NpgsqlConnection(restrictedConnectionString);
        await restrictedConnection.OpenAsync();

        var updateAction = async () => await ExecuteNonQueryAsync(
            restrictedConnection,
            "UPDATE system.audit_records SET action = 'Tampered' WHERE id = @id",
            command => command.Parameters.AddWithValue("id", recordId));
        var updateException = await Assert.ThrowsAsync<PostgresException>(updateAction);
        Assert.Equal("42501", updateException.SqlState);

        var deleteAction = async () => await ExecuteNonQueryAsync(
            restrictedConnection,
            "DELETE FROM system.audit_records WHERE id = @id",
            command => command.Parameters.AddWithValue("id", recordId));
        var deleteException = await Assert.ThrowsAsync<PostgresException>(deleteAction);
        Assert.Equal("42501", deleteException.SqlState);

        // The restricted role's grant genuinely includes SELECT/INSERT — the failures above are
        // specifically about UPDATE/DELETE, not a broader lockout.
        await using (var verifyReader = await ExecuteReaderAsync(
            restrictedConnection,
            "SELECT action FROM system.audit_records WHERE id = @id",
            command => command.Parameters.AddWithValue("id", recordId)))
        {
            Assert.True(await verifyReader.ReadAsync());
            Assert.Equal("Seed.Action", verifyReader.GetString(0));
        }

        await restrictedConnection.CloseAsync();
        await ExecuteNonQueryAsync(adminConnection, $"REVOKE ALL PRIVILEGES ON system.audit_records FROM {restrictedRole}");
        await ExecuteNonQueryAsync(adminConnection, $"REVOKE ALL PRIVILEGES ON SCHEMA system FROM {restrictedRole}");
        await ExecuteNonQueryAsync(adminConnection, $"DROP ROLE {restrictedRole}");
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        var connection = new NpgsqlConnection(configuration.GetConnectionString("DefaultConnection"));
        await connection.OpenAsync();
        return connection;
    }

    private string BuildConnectionStringForRole(string role, string password)
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        var builder = new NpgsqlConnectionStringBuilder(configuration.GetConnectionString("DefaultConnection"))
        {
            Username = role,
            Password = password
        };
        return builder.ConnectionString;
    }

    private static async Task ExecuteNonQueryAsync(NpgsqlConnection connection, string sql, Action<NpgsqlCommand>? configure = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        configure?.Invoke(command);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<NpgsqlDataReader> ExecuteReaderAsync(NpgsqlConnection connection, string sql, Action<NpgsqlCommand>? configure = null)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        configure?.Invoke(command);
        return await command.ExecuteReaderAsync();
    }
}

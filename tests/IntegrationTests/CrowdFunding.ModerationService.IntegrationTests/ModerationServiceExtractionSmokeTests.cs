extern alias MonolithHost;
extern alias ModerationHost;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

using MonolithProgram = MonolithHost::Program;
using ModerationProgram = ModerationHost::Program;
using AdminSeeder = MonolithHost::CrowdFunding.API.Migrations.AdminSeeder;
using MigrationRunner = MonolithHost::CrowdFunding.API.Migrations.MigrationRunner;
using RegisterUserRequest = MonolithHost::CrowdFunding.API.Contracts.Identity.RegisterUserRequest;
using LoginUserRequest = MonolithHost::CrowdFunding.API.Contracts.Identity.LoginUserRequest;
using LoginUserResponse = MonolithHost::CrowdFunding.API.Contracts.Identity.LoginUserResponse;
using CampaignReview = ModerationHost::CrowdFunding.Modules.Moderation.Domain.Aggregates.CampaignReview;
using ICampaignReviewRepository = ModerationHost::CrowdFunding.Modules.Moderation.Application.Abstractions.Persistence.ICampaignReviewRepository;
using IModerationTransactionExecutor = ModerationHost::CrowdFunding.Modules.Moderation.Application.Abstractions.Transactions.IModerationTransactionExecutor;
using ApproveCampaignReviewResult = ModerationHost::CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.ApproveCampaignReview.ApproveCampaignReviewResult;
using JwksSigningKeyResolver = ModerationHost::CrowdFunding.Moderation.Service.Security.JwksSigningKeyResolver;

namespace CrowdFunding.ModerationService.IntegrationTests;

/// <summary>
/// TICKET-029's automated proof: a JWT issued by the monolith's Identity module authenticates
/// successfully against <c>CrowdFunding.Moderation.Service</c> — an entirely separate ASP.NET
/// Core host, process, and DI container — purely by fetching the monolith's public JWKS over
/// HTTP. Both hosts run as real <see cref="WebApplicationFactory{TEntryPoint}"/> instances so
/// this exercises the actual extracted assembly, not a hand-rolled fake. The two hosts share one
/// Postgres container (module-per-schema, exactly as TICKET-026 established) but no in-process
/// call ever crosses between them — extraction is proven by literally never referencing
/// <c>MonolithProgram</c>'s DI container from <c>ModerationProgram</c>'s request pipeline.
/// </summary>
public sealed class ModerationServiceExtractionSmokeTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("crowdfunding_extraction_smoke")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private MonolithApiFactory _monolith = null!;
    private ModerationServiceFactory _moderationService = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _monolith = new MonolithApiFactory(_postgres.GetConnectionString());
        _ = _monolith.Server;
        await MigrationRunner.RunAsync(_monolith.Services);

        _moderationService = new ModerationServiceFactory(_postgres.GetConnectionString(), _monolith);
        _ = _moderationService.Server;
    }

    public async Task DisposeAsync()
    {
        await _moderationService.DisposeAsync();
        await _monolith.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task AdminJwtIssuedByTheMonolith_ShouldAuthorizeApprovalOnTheExtractedModerationService()
    {
        using var identityClient = _monolith.CreateClient();

        var adminEmail = $"extraction-admin-{Guid.NewGuid():N}@example.com";
        await AdminSeeder.RunAsync(_monolith.Services, adminEmail, "P@ssw0rd123!", "Extraction Admin");

        var loginResponse = await identityClient.PostAsJsonAsync(
            "api/identity/login", new LoginUserRequest(adminEmail, "P@ssw0rd123!"));
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginUserResponse>();

        var campaignId = Guid.NewGuid();
        using (var scope = _moderationService.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<ICampaignReviewRepository>();
            var transactionExecutor = scope.ServiceProvider.GetRequiredService<IModerationTransactionExecutor>();
            var review = CampaignReview.Create(campaignId, DateTime.UtcNow);
            await transactionExecutor.ExecuteAsync(ct => repository.AddAsync(review, ct), CancellationToken.None);
        }

        using var moderationClient = _moderationService.CreateClient();
        moderationClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);

        var approveResponse = await moderationClient.PostAsJsonAsync(
            $"api/moderation/reviews/{campaignId}/approve", new { Notes = (string?)null });

        var body = await approveResponse.Content.ReadAsStringAsync();
        Assert.True(approveResponse.IsSuccessStatusCode, $"Expected success, got {approveResponse.StatusCode}: {body}");

        var approved = await approveResponse.Content.ReadFromJsonAsync<ApproveCampaignReviewResult>();
        Assert.Equal("Approved", approved!.Status);
    }

    [Fact]
    public async Task RequestWithNoBearerToken_ShouldBeRejectedByTheExtractedModerationService()
    {
        using var moderationClient = _moderationService.CreateClient();

        var response = await moderationClient.GetAsync($"api/moderation/reviews/{Guid.NewGuid()}");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed class MonolithApiFactory : WebApplicationFactory<MonolithProgram>
    {
        private readonly string _connectionString;

        public MonolithApiFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseContentRoot(AppContext.BaseDirectory);
            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
            builder.UseSetting("ConnectionStrings:Redis", "localhost:6379");
            builder.UseSetting("Jwt:Issuer", "CrowdFunding.ExtractionSmokeTest");
            builder.UseSetting("Jwt:Audience", "CrowdFunding.ExtractionSmokeTest.Client");
            builder.UseSetting("Jwt:ExpirationMinutes", "60");
            builder.UseSetting("OpenMeter:BaseUrl", "https://openmeter.cloud");
            builder.UseSetting("RateLimiting:Auth:PermitLimit", "100000");
            builder.UseSetting("RateLimiting:Payment:PermitLimit", "100000");
        }
    }

    private sealed class ModerationServiceFactory : WebApplicationFactory<ModerationProgram>
    {
        private readonly string _connectionString;
        private readonly MonolithApiFactory _monolith;

        public ModerationServiceFactory(string connectionString, MonolithApiFactory monolith)
        {
            _connectionString = connectionString;
            _monolith = monolith;
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseContentRoot(AppContext.BaseDirectory);
            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:ModerationDb", _connectionString);
            builder.UseSetting("Authentication:Issuer", "CrowdFunding.ExtractionSmokeTest");
            builder.UseSetting("Authentication:JwksUri", "http://monolith.internal/.well-known/jwks.json");
            builder.UseSetting("Messaging:Provider", "InProcess");

            builder.ConfigureTestServices(services =>
            {
                // Redirects the resolver's HttpClient into the monolith's own in-memory
                // TestServer instead of a real socket — the ONLY seam between the two hosts in
                // this test, and it is exactly the seam TICKET-029 claims: an HTTP fetch of
                // public key material, nothing else.
                services.AddHttpClient<JwksSigningKeyResolver>()
                    .ConfigurePrimaryHttpMessageHandler(() => _monolith.Server.CreateHandler());
            });
        }
    }
}

using System.Text;
using CrowdFunding.API.Documentation;
using CrowdFunding.API.Mapping;
using CrowdFunding.API.Migrations;
using CrowdFunding.API.Observability;
using CrowdFunding.API.RateLimiting;
using CrowdFunding.API.RealTime;
using CrowdFunding.API.Security;
using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.BuildingBlocks.Infrastructure.Messaging;
using CrowdFunding.BuildingBlocks.Infrastructure.Metering;
using CrowdFunding.Modules.CampaignUpdates.Application.DependencyInjection;
using CrowdFunding.Modules.CampaignUpdates.Infrastructure.DependencyInjection;
using CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;
using CrowdFunding.Modules.Campaigns.Application.DependencyInjection;
using CrowdFunding.Modules.Campaigns.Infrastructure.DependencyInjection;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Contributions.Application.DependencyInjection;
using CrowdFunding.Modules.Contributions.Infrastructure.DependencyInjection;
using CrowdFunding.Modules.Contributions.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Identity.Application.Abstractions.Services;
using CrowdFunding.Modules.Identity.Application.DependencyInjection;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using CrowdFunding.Modules.Identity.Infrastructure.DependencyInjection;
using CrowdFunding.Modules.Identity.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Moderation.Application.DependencyInjection;
using CrowdFunding.Modules.Moderation.Infrastructure.DependencyInjection;
using CrowdFunding.Modules.Moderation.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Notifications.Application.DependencyInjection;
using CrowdFunding.Modules.Notifications.Infrastructure.DependencyInjection;
using CrowdFunding.Modules.Notifications.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Samples.RosettaStone;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCreated;
using CrowdFunding.Modules.Contributions.Contracts.Events.ContributionPaymentConfirmed;
using CrowdFunding.Modules.Moderation.Contracts.Events.CampaignReviewApproved;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerUI;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseCrowdFundingSerilog();

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer was not configured.");
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience was not configured.");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCrowdFundingSwagger();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddCrowdFundingRateLimiting(builder.Configuration);
builder.Services.Configure<PaymentGatewayWebhookOptions>(
    builder.Configuration.GetSection(PaymentGatewayWebhookOptions.SectionName));
builder.Services.Configure<CloudFunctionWebhookOptions>(
    builder.Configuration.GetSection(CloudFunctionWebhookOptions.SectionName));

builder.Services.AddHealthChecks()
    .AddCheck<DbContextHealthCheck<CampaignsDbContext>>("campaigns-db", tags: ["ready"])
    .AddCheck<DbContextHealthCheck<ContributionsDbContext>>("contributions-db", tags: ["ready"])
    .AddCheck<DbContextHealthCheck<IdentityDbContext>>("identity-db", tags: ["ready"])
    .AddCheck<DbContextHealthCheck<ModerationDbContext>>("moderation-db", tags: ["ready"])
    .AddCheck<DbContextHealthCheck<NotificationsDbContext>>("notifications-db", tags: ["ready"])
    .AddCheck<DbContextHealthCheck<CampaignUpdatesDbContext>>("campaign-updates-db", tags: ["ready"]);

builder.Services.AddOpenMeterMetering(builder.Configuration);

builder.Services.AddSignalR();
builder.Services.AddScoped<ICampaignRealtimeNotifier, SignalRCampaignRealtimeNotifier>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
builder.Services.AddCrowdFundingMessaging(builder.Configuration);
builder.Services.AddScoped<ICommandDispatcher, CommandDispatcher>();
builder.Services.AddScoped<IQueryDispatcher, QueryDispatcher>();

builder.Services.AddRequestHandlersFromAssemblies(
    typeof(IdentityApplicationDependencyInjection).Assembly,
    typeof(CampaignsApplicationDependencyInjection).Assembly,
    typeof(ContributionsApplicationDependencyInjection).Assembly,
    typeof(ModerationApplicationDependencyInjection).Assembly,
    typeof(NotificationsApplicationDependencyInjection).Assembly,
    typeof(CampaignUpdatesApplicationDependencyInjection).Assembly);

builder.Services.AddEventTypeRegistry(
    typeof(CampaignCreatedApplicationEvent).Assembly,
    typeof(ContributionPaymentConfirmedApplicationEvent).Assembly,
    typeof(CampaignReviewApprovedApplicationEvent).Assembly);

builder.Services.AddEventHandlersFromAssemblies(
    typeof(IdentityApplicationDependencyInjection).Assembly,
    typeof(CampaignsApplicationDependencyInjection).Assembly,
    typeof(ContributionsApplicationDependencyInjection).Assembly,
    typeof(ModerationApplicationDependencyInjection).Assembly,
    typeof(NotificationsApplicationDependencyInjection).Assembly,
    typeof(CampaignUpdatesApplicationDependencyInjection).Assembly);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        // TICKET-036: a JWT's signature/lifetime validation above is entirely stateless and
        // cannot itself be revoked before natural expiration. This event adds the one additional
        // check needed for instant global revocation across every module — a Redis lookup keyed
        // by the token's own security_stamp claim, not a database read — so logout, refresh-token
        // reuse detection, and (when wired up) account deactivation can invalidate an
        // already-issued, not-yet-expired access token immediately.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userIdClaim = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var stampClaim = context.Principal?.FindFirst(CustomClaimTypes.SecurityStamp)?.Value;

                if (!Guid.TryParse(userIdClaim, out var userId) || !Guid.TryParse(stampClaim, out var stamp))
                {
                    context.Fail("Token is missing required claims.");
                    return;
                }

                var revocationStore = context.HttpContext.RequestServices.GetRequiredService<ISecurityStampRevocationStore>();

                if (await revocationStore.IsRevokedAsync(userId, stamp, context.HttpContext.RequestAborted))
                {
                    context.Fail("Token has been revoked.");
                }
            }
        };
    });

// Asymmetric ES256 verification (improvement.md §2.8/§3.8 #2): the resolver reads only the
// public half of the signing key from ISigningKeyStore. A downstream verifier that only had the
// public JWKS (see the /.well-known/jwks.json mapping below) could never forge a token, unlike
// the previous symmetric HMAC secret shared with every verifier.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<ISigningKeyStore>((options, signingKeyStore) =>
    {
        options.TokenValidationParameters.IssuerSigningKeyResolver = (_, _, kid, _) =>
            signingKeyStore.GetPublicSigningKeys()
                .Where(key => kid is null || key.Kid == kid)
                .Select(key => (SecurityKey)new ECDsaSecurityKey(key.Key) { KeyId = key.Kid });
    });

builder.Services.AddAuthorization(options =>
{
    foreach (var permission in PermissionConstants.All)
    {
        options.AddPolicy(permission, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim(CustomClaimTypes.Permission, permission);
        });
    }
});

builder.Services.AddIdentityApplication();
builder.Services.AddIdentityInfrastructure(builder.Configuration);

builder.Services.AddCampaignsApplication();
builder.Services.AddCampaignsInfrastructure(builder.Configuration);

builder.Services.AddContributionsApplication();
builder.Services.AddContributionsInfrastructure(builder.Configuration);

builder.Services.AddModerationApplication();
builder.Services.AddModerationInfrastructure(builder.Configuration);

builder.Services.AddNotificationsApplication();
builder.Services.AddNotificationsInfrastructure(builder.Configuration);
builder.Services.AddCampaignUpdatesApplication();
builder.Services.AddCampaignUpdatesInfrastructure(builder.Configuration);

builder.Services.AddRosettaStoneSample(builder.Configuration);

var typeAdapterConfig = new TypeAdapterConfig();
CampaignsMappingConfig.Register(typeAdapterConfig);
ContributionsMappingConfig.Register(typeAdapterConfig);
IdentityMappingConfig.Register(typeAdapterConfig);
ModerationMappingConfig.Register(typeAdapterConfig);
CampaignUpdatesMappingConfig.Register(typeAdapterConfig);

builder.Services.AddSingleton(typeAdapterConfig);
builder.Services.AddScoped<IMapper, ServiceMapper>();

var app = builder.Build();

// Explicit CLI migration step for CI/CD pipelines and container init-jobs: `dotnet run -- migrate`
// applies every module's migrations once and exits, instead of relying on an app instance
// booting under IsDevelopment() to run them implicitly (unsafe with >=2 production replicas
// racing concurrent DDL — improvement.md §2.7).
if (args.Contains("migrate"))
{
    await MigrationRunner.RunAsync(app.Services);
    return;
}

// `dotnet run -- seed-admin <email> <password> <displayName>`: creates the initial
// Administrator out-of-band, run once by an operator. Public self-registration
// (RegisterUserCommandHandler) never grants Admin — see AdminSeeder's remarks for why.
if (args.Length > 0 && args[0] == "seed-admin")
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: dotnet run -- seed-admin <email> <password> <displayName>");
        return;
    }

    await AdminSeeder.RunAsync(app.Services, args[1], args[2], args[3]);
    return;
}

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    await MigrationRunner.RunAsync(app.Services);
    app.UseCrowdFundingSwagger();
}

await using (var scope = app.Services.CreateAsyncScope())
{
    var signingKeyStore = scope.ServiceProvider.GetRequiredService<ISigningKeyStore>();
    await signingKeyStore.WarmUpAsync(CancellationToken.None);
}

app.UseHttpsRedirection();

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthCheckResponseWriter.WriteResponseAsync,
}).WithTags("System");

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteResponseAsync,
}).WithTags("System");

app.MapGet("/.well-known/jwks.json", JwksEndpoint.Get)
    .AllowAnonymous()
    .WithTags("System");

app.MapRosettaStoneSample();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<CampaignHub>("/hubs/campaigns");
app.Run();

// Exposes the generated Program class so WebApplicationFactory<Program> can reference it
// from CrowdFunding.IntegrationTests (top-level statements otherwise produce an internal type).
public partial class Program;
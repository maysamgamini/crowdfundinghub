using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.BuildingBlocks.Infrastructure.Messaging;
using CrowdFunding.Moderation.Service.Observability;
using CrowdFunding.Moderation.Service.Security;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCreated;
using CrowdFunding.Modules.Moderation.Application.DependencyInjection;
using CrowdFunding.Modules.Moderation.Contracts.Events.CampaignReviewApproved;
using CrowdFunding.Modules.Moderation.Infrastructure.DependencyInjection;
using CrowdFunding.Modules.Moderation.Infrastructure.Persistence.DbContexts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var jwtIssuer = builder.Configuration["Authentication:Issuer"]
    ?? throw new InvalidOperationException("Authentication:Issuer was not configured.");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.SwaggerDoc("v1", new() { Title = "CrowdFunding.Moderation.Service", Version = "v1" }));

builder.Services.AddExceptionHandler<ServiceExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddScoped<ICommandDispatcher, CommandDispatcher>();
builder.Services.AddScoped<IQueryDispatcher, QueryDispatcher>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, ServiceCurrentUser>();

// Registers ONLY the pre-existing Moderation module wiring plus the reused CloudEvents/outbox
// event contract of the one upstream event Moderation already consumed in-process
// (CampaignCreatedApplicationEvent) — zero new command/query handlers, zero rewritten business
// rules. This is TICKET-029's whole point: extraction requires only a new host assembly.
builder.Services.AddRequestHandlersFromAssemblies(typeof(ModerationApplicationDependencyInjection).Assembly);
builder.Services.AddEventTypeRegistry(
    typeof(CampaignCreatedApplicationEvent).Assembly,
    typeof(CampaignReviewApprovedApplicationEvent).Assembly);
builder.Services.AddEventHandlersFromAssemblies(typeof(ModerationApplicationDependencyInjection).Assembly);

builder.Services.AddModerationApplication();
builder.Services.AddModerationInfrastructure(builder.Configuration);
builder.Services.AddCrowdFundingMessaging(builder.Configuration);

builder.Services.Configure<JwksOptions>(builder.Configuration.GetSection(JwksOptions.SectionName));
builder.Services.AddHttpClient<JwksSigningKeyResolver>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            // Decentralized authentication (TICKET-029 acceptance criterion #3): audience
            // validation is intentionally skipped, matching the ticket's own reference
            // Program.cs — an extracted service trusts any token the shared Identity issuer
            // signed, regardless of which client audience it was originally minted for.
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

// Same pattern the monolith uses to wire its own ISigningKeyStore-backed resolver: post-configure
// the named JwtBearerOptions instance with a resolver pulled from DI (JwksSigningKeyResolver
// needs an HttpClient, so it can't be constructed inline in the AddJwtBearer callback above).
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<JwksSigningKeyResolver>((options, resolver) =>
    {
        options.TokenValidationParameters.IssuerSigningKeyResolver = resolver.ResolveSigningKeys;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (args.Contains("migrate"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<ModerationDbContext>().Database.MigrateAsync();
    return;
}

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<ModerationDbContext>().Database.MigrateAsync();

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" })).AllowAnonymous();

app.Run();

/// <summary>
/// Exposes the generated Program class so WebApplicationFactory&lt;Program&gt; can reference it
/// from an integration test project, matching the monolith's own pattern.
/// </summary>
public partial class Program;

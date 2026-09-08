using System.Text;
using CrowdFunding.API.Background;
using CrowdFunding.API.Mapping;
using CrowdFunding.API.Migrations;
using CrowdFunding.API.Observability;
using CrowdFunding.API.Security;
using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.BuildingBlocks.Infrastructure.Events;
using CrowdFunding.Modules.CampaignUpdates.Application.DependencyInjection;
using CrowdFunding.Modules.Campaigns.Application.DependencyInjection;
using CrowdFunding.Modules.Campaigns.Infrastructure.DependencyInjection;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Contributions.Application.DependencyInjection;
using CrowdFunding.Modules.Contributions.Infrastructure.DependencyInjection;
using CrowdFunding.Modules.Contributions.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Identity.Application.DependencyInjection;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using CrowdFunding.Modules.Identity.Infrastructure.DependencyInjection;
using CrowdFunding.Modules.Identity.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Moderation.Application.DependencyInjection;
using CrowdFunding.Modules.Moderation.Infrastructure.DependencyInjection;
using CrowdFunding.Modules.Moderation.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Notifications.Application.DependencyInjection;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseCrowdFundingSerilog();

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer was not configured.");
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience was not configured.");
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey was not configured.");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks()
    .AddCheck<DbContextHealthCheck<CampaignsDbContext>>("campaigns-db", tags: ["ready"])
    .AddCheck<DbContextHealthCheck<ContributionsDbContext>>("contributions-db", tags: ["ready"])
    .AddCheck<DbContextHealthCheck<IdentityDbContext>>("identity-db", tags: ["ready"])
    .AddCheck<DbContextHealthCheck<ModerationDbContext>>("moderation-db", tags: ["ready"]);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
builder.Services.AddScoped<IEventPublisher, ServiceProviderEventPublisher>();
builder.Services.AddScoped<ICommandDispatcher, CommandDispatcher>();
builder.Services.AddScoped<IQueryDispatcher, QueryDispatcher>();
builder.Services.AddHostedService<OutboxProcessorBackgroundService>();

builder.Services.AddRequestHandlersFromAssemblies(
    typeof(IdentityApplicationDependencyInjection).Assembly,
    typeof(CampaignsApplicationDependencyInjection).Assembly,
    typeof(ContributionsApplicationDependencyInjection).Assembly,
    typeof(ModerationApplicationDependencyInjection).Assembly,
    typeof(NotificationsApplicationDependencyInjection).Assembly,
    typeof(CampaignUpdatesApplicationDependencyInjection).Assembly);

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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
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
builder.Services.AddCampaignUpdatesApplication();

var typeAdapterConfig = new TypeAdapterConfig();
CampaignsMappingConfig.Register(typeAdapterConfig);
ContributionsMappingConfig.Register(typeAdapterConfig);
IdentityMappingConfig.Register(typeAdapterConfig);
ModerationMappingConfig.Register(typeAdapterConfig);

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

if (app.Environment.IsDevelopment())
{
    await MigrationRunner.RunAsync(app.Services);
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthCheckResponseWriter.WriteResponseAsync,
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteResponseAsync,
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
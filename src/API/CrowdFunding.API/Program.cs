using System.Text;
using CrowdFunding.API.Mapping;
using CrowdFunding.API.Middleware;
using CrowdFunding.API.Security;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Campaigns.Application.DependencyInjection;
using CrowdFunding.Modules.Campaigns.Infrastructure.DependencyInjection;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Contributions.Application.DependencyInjection;
using CrowdFunding.Modules.Contributions.Infrastructure.DependencyInjection;
using CrowdFunding.Modules.Contributions.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using CrowdFunding.Modules.Identity.Application.DependencyInjection;
using CrowdFunding.Modules.Identity.Infrastructure.DependencyInjection;
using CrowdFunding.Modules.Identity.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Moderation.Application.DependencyInjection;
using CrowdFunding.Modules.Moderation.Infrastructure.DependencyInjection;
using CrowdFunding.Modules.Moderation.Infrastructure.Persistence.DbContexts;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer was not configured.");
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience was not configured.");
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey was not configured.");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

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

var typeAdapterConfig = new TypeAdapterConfig();
CampaignsMappingConfig.Register(typeAdapterConfig);
ContributionsMappingConfig.Register(typeAdapterConfig);
IdentityMappingConfig.Register(typeAdapterConfig);
ModerationMappingConfig.Register(typeAdapterConfig);

builder.Services.AddSingleton(typeAdapterConfig);
builder.Services.AddScoped<IMapper, ServiceMapper>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await app.ApplyMigrationsAsync();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static class StartupDatabaseMigrationExtensions
{
    public static async Task ApplyMigrationsAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();

        await MigrateAsync<CampaignsDbContext>(scope.ServiceProvider);
        await MigrateAsync<ContributionsDbContext>(scope.ServiceProvider);
        await MigrateAsync<IdentityDbContext>(scope.ServiceProvider);
        await MigrateAsync<ModerationDbContext>(scope.ServiceProvider);
    }

    private static async Task MigrateAsync<TContext>(IServiceProvider serviceProvider)
        where TContext : DbContext
    {
        var dbContext = serviceProvider.GetRequiredService<TContext>();
        await dbContext.Database.MigrateAsync();
    }
}

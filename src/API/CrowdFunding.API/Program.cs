using System.Text;
using CrowdFunding.API.Mapping;
using CrowdFunding.API.Middleware;
using CrowdFunding.API.Security;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Campaigns.Infrastructure.DependencyInjection;
using CrowdFunding.Modules.Contributions.Infrastructure.DependencyInjection;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using CrowdFunding.Modules.Identity.Infrastructure.DependencyInjection;
using CrowdFunding.Modules.Moderation.Infrastructure.DependencyInjection;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddCampaignsModule(builder.Configuration);
builder.Services.AddContributionsModule(builder.Configuration);
builder.Services.AddModerationModule(builder.Configuration);

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
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

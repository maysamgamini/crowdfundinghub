using CrowdFunding.API.Mapping;
using CrowdFunding.API.Middleware;
using CrowdFunding.Modules.Campaigns.Infrastructure.DependencyInjection;
using CrowdFunding.Modules.Contributions.Infrastructure.DependencyInjection;
using CrowdFunding.Modules.Moderation.Infrastructure.DependencyInjection;
using Mapster;
using MapsterMapper;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCampaignsModule(builder.Configuration);
builder.Services.AddContributionsModule(builder.Configuration);
builder.Services.AddModerationModule(builder.Configuration);

var typeAdapterConfig = new TypeAdapterConfig();
CampaignsMappingConfig.Register(typeAdapterConfig);
ContributionsMappingConfig.Register(typeAdapterConfig);
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

app.UseAuthorization();

app.MapControllers();

app.Run();

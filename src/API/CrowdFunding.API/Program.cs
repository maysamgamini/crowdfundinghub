using CrowdFunding.API.Mapping;
using CrowdFunding.API.Middleware;
using CrowdFunding.Modules.Campaigns.Infrastructure.DependencyInjection;
using Mapster;
using MapsterMapper;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCampaignsModule(builder.Configuration);

var typeAdapterConfig = new TypeAdapterConfig();
CampaignsMappingConfig.Register(typeAdapterConfig);

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

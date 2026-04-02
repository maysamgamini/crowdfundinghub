using CrowdFunding.API.Mapping;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CreateCampaign;
using CrowdFunding.Modules.Campaigns.Infrastructure.DependencyInjection;
using Mapster;
using MapsterMapper;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCampaignsModule(builder.Configuration);

builder.Services.AddScoped<CreateCampaignCommandHandler>();
builder.Services.AddScoped<IValidator<CreateCampaignCommand>, CreateCampaignCommandValidator>();

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

app.UseAuthorization();

app.MapControllers();

app.Run();
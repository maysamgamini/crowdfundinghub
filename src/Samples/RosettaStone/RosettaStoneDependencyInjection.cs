using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Infrastructure.Configuration;
using CrowdFunding.Samples.RosettaStone.Persistence;
using CrowdFunding.Samples.RosettaStone.PragmaticCqrs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.Samples.RosettaStone;

/// <summary>
/// Wires the Rosetta Stone sample into the host. Deliberately registered as its own isolated
/// slice — deleting this call and this project removes the sample with zero blast radius on
/// any production module.
/// </summary>
public static class RosettaStoneDependencyInjection
{
    public static IServiceCollection AddRosettaStoneSample(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredModuleConnectionString("RosettaStoneDb");

        services.AddDbContext<RosettaStoneDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "rosetta")));

        services.AddScoped<CreateCampaignCommandValidator>();
        services.AddScoped<ICommandHandler<CreateCampaignCommand, CreateCampaignResult>, CreateCampaignCommandHandler>();

        return services;
    }
}

using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Samples.RosettaStone.Persistence;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

namespace CrowdFunding.Samples.RosettaStone;

/// <summary>
/// Standalone host for the Rosetta Stone teaching sample: Tier 1 (Minimal API CRUD) and Tier 2
/// (Pragmatic CQRS) of one business requirement, runnable on their own. Tier 3, the Rich DDD +
/// Outbox implementation, is the production endpoint in CrowdFunding.API (POST /api/campaigns).
/// </summary>
/// <remarks>
/// An explicit, namespaced entry point rather than top-level statements: .NET 10 makes a
/// top-level <c>Program</c> public, which would collide with the API's <c>Program</c> in the
/// integration tests that boot both hosts. The sample applies its own migrations on startup,
/// which suits a single-instance teaching host and is deliberately not what the production API
/// does.
/// </remarks>
public sealed class RosettaStoneHost
{
    private RosettaStoneHost()
    {
    }

    /// <summary>
    /// Runs the sample host.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>A task that completes when the host shuts down.</returns>
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddProblemDetails();
        builder.Services.AddOpenApi();
        builder.Services.AddScoped<ICommandDispatcher, CommandDispatcher>();
        builder.Services.AddRosettaStoneSample(builder.Configuration);

        var app = builder.Build();

        await using (var scope = app.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<RosettaStoneDbContext>().Database.MigrateAsync();
        }

        app.UseExceptionHandler();
        app.UseStatusCodePages();

        app.MapOpenApi();
        app.MapScalarApiReference(options => options.WithTitle("Rosetta Stone sample"));
        app.MapRosettaStoneSample();

        await app.RunAsync();
    }
}

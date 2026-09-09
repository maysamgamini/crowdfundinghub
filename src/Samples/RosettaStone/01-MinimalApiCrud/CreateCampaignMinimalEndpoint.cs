using CrowdFunding.Samples.RosettaStone.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrowdFunding.Samples.RosettaStone.MinimalApiCrud;

/// <summary>
/// Tier 1: Minimal API single-file slice. One file, no validator class, no command, no
/// dispatcher, no domain aggregate, no outbox — the entire "create a campaign" use case,
/// end to end, in the number of lines a config/lookup/prototype table insert actually deserves.
/// </summary>
public static class CreateCampaignMinimalEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/tier1-minimal-api", async (
            CreateCampaignRequest req,
            RosettaStoneDbContext db,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Title) || req.TargetAmount <= 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Title"] = ["Title is required and target amount must be positive."]
                });
            }

            var record = new CampaignRecord
            {
                Id = Guid.NewGuid(),
                Title = req.Title,
                Story = req.Story,
                TargetAmount = req.TargetAmount,
                Currency = req.Currency,
                CreatedAtUtc = DateTime.UtcNow
            };

            db.CampaignRecords.Add(record);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/rosetta/v1/tier1-minimal-api/{record.Id}", new { record.Id });
        })
        .WithSummary("Tier 1: Minimal API single-file slice");
    }

    public sealed record CreateCampaignRequest(string Title, string Story, decimal TargetAmount, string Currency);
}

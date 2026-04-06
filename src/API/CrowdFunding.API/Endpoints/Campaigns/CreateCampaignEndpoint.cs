using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CreateCampaign;

namespace CrowdFunding.API.Endpoints.Campaigns;

public static class CreateCampaignEndpoint
{
    public static IEndpointRouteBuilder MapCreateCampaign(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/campaigns", async (
            CreateCampaignRequest request,
            CreateCampaignCommandHandler handler,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateCampaignCommand(
                request.OwnerId,
                request.Title,
                request.Story,
                request.Category,
                request.GoalAmount,
                request.Currency,
                request.DeadlineUtc);

            var result = await handler.Handle(command, cancellationToken);

            return Results.Created($"/api/campaigns/{result.CampaignId}",
                new CreateCampaignResponse(result.CampaignId));
        });

        return app;
    }
}
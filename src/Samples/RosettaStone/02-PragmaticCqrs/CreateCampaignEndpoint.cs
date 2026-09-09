using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Samples.RosettaStone;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrowdFunding.Samples.RosettaStone.PragmaticCqrs;

/// <summary>
/// Tier 2 endpoint: validate, then dispatch through the same <c>ICommandDispatcher</c> every
/// module in the monolith already uses. No controller, no MediatR pipeline behaviors, no
/// aggregate — just a validated command handed to a handler.
/// </summary>
public static class CreateCampaignEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/tier2-pragmatic-cqrs", async (
            CreateCampaignCommand command,
            CreateCampaignCommandValidator validator,
            ICommandDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var validationResult = await validator.ValidateAsync(command, ct);
            if (!validationResult.IsValid)
            {
                return RosettaValidationProblem.FromFluentValidation(validationResult);
            }

            var result = await dispatcher.SendAsync<CreateCampaignResult>(command, ct);

            return Results.Created($"/rosetta/v1/tier2-pragmatic-cqrs/{result.Id}", result);
        })
        .WithSummary("Tier 2: Pragmatic CQRS slice");
    }
}

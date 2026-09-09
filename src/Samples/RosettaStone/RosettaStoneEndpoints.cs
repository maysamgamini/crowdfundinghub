using CrowdFunding.Samples.RosettaStone.MinimalApiCrud;
using CrowdFunding.Samples.RosettaStone.PragmaticCqrs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrowdFunding.Samples.RosettaStone;

/// <summary>
/// Maps every Rosetta Stone tier under one route group and Swagger tag so a reader can compare
/// Tier 1 and Tier 2 side by side. Tier 3 has no endpoint here — it is the monolith's existing
/// <c>POST /api/campaigns</c>, unmodified; see <c>03-RichDomainModel/README.md</c>.
/// </summary>
public static class RosettaStoneEndpoints
{
    public static void MapRosettaStoneSample(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/rosetta/v1/campaigns").WithTags("RosettaStone");

        CreateCampaignMinimalEndpoint.Map(group);
        CreateCampaignEndpoint.Map(group);
    }
}

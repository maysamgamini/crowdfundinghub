using CrowdFunding.API.Contracts.Pledges;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Queries.GetCampaignPayout;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrowdFunding.API.Controllers;

/// <summary>Exposes the payout of a campaign to its creator.</summary>
[ApiController]
[Route("api/campaigns/{campaignId:guid}/payout")]
[Tags("Pledges")]
public sealed class PayoutController : ControllerBase
{
    private readonly IQueryDispatcher _queryDispatcher;

    public PayoutController(IQueryDispatcher queryDispatcher)
    {
        _queryDispatcher = queryDispatcher;
    }

    /// <summary>Reads the payout of a campaign: the amounts, the status, and the ledger lines behind the balance.</summary>
    /// <param name="campaignId">The unique identifier of the campaign.</param>
    /// <param name="cancellationToken">Cancellation token for asynchronous operation.</param>
    /// <response code="200">The payout. Only the campaign's creator, or a payments manager, can read it.</response>
    /// <response code="401">The caller is not signed in.</response>
    /// <response code="404">The campaign has no payout, or the caller may not read it.</response>
    [Authorize]
    [HttpGet]
    [ProducesResponseType(typeof(GetCampaignPayoutResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetCampaignPayoutResponse>> Get([FromRoute] Guid campaignId, CancellationToken cancellationToken)
    {
        var result = await _queryDispatcher.QueryAsync<GetCampaignPayoutResult>(new GetCampaignPayoutQuery(campaignId), cancellationToken);

        return Ok(new GetCampaignPayoutResponse(
            result.PayoutId,
            result.CampaignId,
            result.Status,
            result.Currency,
            result.GrossAmount,
            result.FeeAmount,
            result.NetAmount,
            result.PaidAtUtc,
            result.Balance,
            result.Lines.Select(x => new PayoutLedgerLineResponse(x.Kind, x.Amount, x.PledgeId, x.CreatedAtUtc)).ToList()));
    }
}

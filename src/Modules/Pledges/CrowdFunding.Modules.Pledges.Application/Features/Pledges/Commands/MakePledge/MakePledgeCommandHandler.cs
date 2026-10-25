using CrowdFunding.BuildingBlocks.Application.Exceptions;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Services;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Pledges.Domain.Aggregates;
using CrowdFunding.Modules.Identity.Contracts.Authorization;

namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.MakePledge;

/// <summary>
/// Handles Make Pledge command requests.
/// </summary>
public sealed class MakePledgeCommandHandler : ICommandHandler<MakePledgeCommand, MakePledgeResult>
{
    private readonly IActiveCampaignCacheRepository _activeCampaignCacheRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IPledgeDateTimeProvider _dateTimeProvider;
    private readonly IPledgeRepository _pledgeRepository;
    private readonly IPledgeTransactionExecutor _transactionExecutor;
    private readonly IPaymentProvider _paymentProvider;
    private readonly IPaymentSettings _paymentSettings;

    public MakePledgeCommandHandler(
        IActiveCampaignCacheRepository activeCampaignCacheRepository,
        ICurrentUser currentUser,
        IPledgeDateTimeProvider dateTimeProvider,
        IPledgeRepository pledgeRepository,
        IPledgeTransactionExecutor transactionExecutor,
        IPaymentProvider paymentProvider,
        IPaymentSettings paymentSettings)
    {
        _activeCampaignCacheRepository = activeCampaignCacheRepository;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _pledgeRepository = pledgeRepository;
        _transactionExecutor = transactionExecutor;
        _paymentProvider = paymentProvider;
        _paymentSettings = paymentSettings;
    }

    /// <inheritdoc/>
    public async Task<MakePledgeResult> Handle(MakePledgeCommand command, CancellationToken cancellationToken)
    {
        EnsureCanPledge();

        // A retry of an earlier request is answered from what that request created, before any
        // campaign check: the deadline may have passed since the first attempt, and the backer
        // must still be told what happened to the pledge they already made.
        if (command.IdempotencyKey is not null)
        {
            var earlier = await _pledgeRepository.GetByIdempotencyKeyAsync(
                _currentUser.UserId, command.IdempotencyKey, cancellationToken);

            if (earlier is not null)
            {
                return ReplayOf(earlier, command);
            }
        }

        // Reads Pledges' own locally replicated campaign snapshot rather than calling back
        // into Campaigns synchronously (TICKET-023) — see ReplicatedCampaignEventHandlers.
        var campaign = await _activeCampaignCacheRepository.GetAsync(command.CampaignId, cancellationToken);

        if (campaign is null)
        {
            throw new KeyNotFoundException($"Campaign with id '{command.CampaignId}' was not found.");
        }

        if (!campaign.IsActive || campaign.DeadlineUtc <= _dateTimeProvider.UtcNow)
        {
            throw new InvalidOperationException(
                $"Campaign '{command.CampaignId}' cannot accept pledges — it is not active or has passed its deadline.");
        }

        // Reject a currency mismatch here, before any Pledge/payment record exists.
        // Left unchecked, this surfaces much later as Money.Add throwing inside
        // AddPledgeToCampaignCommandHandler — after the payment was already confirmed —
        // permanently dead-lettering the outbox message with no way to credit the campaign
        // without manual intervention.
        if (!string.Equals(campaign.Currency, command.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Pledge currency '{command.Currency}' does not match campaign currency '{campaign.Currency}'.");
        }

        // Reject creator self-pledging to prevent circular fund-washing, artificial trending manipulation,
        // and perk tier inventory hoarding (TICKET-054).
        if (campaign.OwnerId != Guid.Empty && campaign.OwnerId == _currentUser.UserId)
        {
            throw new InvalidOperationException("Campaign creators cannot back or pledge to their own campaigns.");
        }

        // One live pledge per backer per campaign. An idempotency key only stops a retry of the same
        // request; a second tab with a new key is a different request and is refused here, and by a
        // unique index when two of them arrive together. The backer keeps the pledge they have.
        var livePledge = await _pledgeRepository.GetLivePledgeAsync(command.CampaignId, _currentUser.UserId, cancellationToken);

        if (livePledge is not null)
        {
            // A request that arrives together with the one that created this pledge can get here before it
            // sees the key. If the live pledge holds this request's key, this is a retry, not a second pledge.
            if (command.IdempotencyKey is not null && livePledge.IdempotencyKey == command.IdempotencyKey)
            {
                return ReplayOf(livePledge, command);
            }

            throw AlreadyBacking();
        }

        var pledge = Pledge.Create(
            command.CampaignId,
            _currentUser.UserId,
            command.Amount,
            command.Currency,
            _dateTimeProvider.UtcNow,
            command.RewardTierReservationId,
            command.IdempotencyKey);

        string? clientSecret = null;

        if (_paymentSettings.FundingModel == FundingModel.SaveThenCharge)
        {
            // Save now, charge at the deadline: nothing is charged here. The provider starts a card save and the
            // backer's browser finishes it directly with the provider (card details never reach this system).
            // The key is fixed per pledge, so a retry of this call cannot start a second save.
            var setup = await _paymentProvider.CreateSetupIntentAsync(
                pledge.Id, _currentUser.UserId, $"pledge:{pledge.Id:N}:setup", cancellationToken);
            pledge.AttachPaymentIntent(setup.SetupIntentId, "Mock");
            clientSecret = setup.ClientSecret;
        }
        else
        {
            // In a real integration this id/reference comes back from creating a PaymentIntent (or equivalent) with
            // the external gateway before the client is redirected to complete authentication (3DS), see
            // TICKET-033. No live gateway call is made here; this is the correlation key an inbound webhook
            // reconciles against in ReconcilePaymentWebhookCommandHandler.
            pledge.AttachPaymentIntent($"pi_mock_{pledge.Id:N}", "Mock");
        }

        try
        {
            await _transactionExecutor.ExecuteAsync(async ct =>
            {
                await _pledgeRepository.AddAsync(pledge, ct);
            }, cancellationToken);
        }
        catch (AlreadyBackingException)
        {
            // Two indexes can refuse the same insert: the one-live-pledge index and, when a key was sent, the
            // key's own index. Which one the database reports first is not ours to choose, so when the request
            // carried a key, look for a pledge that holds it: that is a retry and gets the first pledge back.
            if (command.IdempotencyKey is not null)
            {
                var holder = await _pledgeRepository.GetByIdempotencyKeyAsync(
                    _currentUser.UserId, command.IdempotencyKey, cancellationToken);

                if (holder is not null)
                {
                    return ReplayOf(holder, command);
                }
            }

            throw AlreadyBacking();
        }
        catch (IdempotencyKeyAlreadyUsedException) when (command.IdempotencyKey is not null)
        {
            // Two requests with the same key arrived together and the other one inserted first.
            // The unique index decided; answer with the winner.
            var winner = await _pledgeRepository.GetByIdempotencyKeyAsync(
                _currentUser.UserId, command.IdempotencyKey, cancellationToken)
                ?? throw new InvalidOperationException("The pledge that holds this idempotency key could not be found.");

            return ReplayOf(winner, command);
        }

        return new MakePledgeResult(pledge.Id, pledge.Status.ToString(), ClientSecret: clientSecret);
    }

    private static ResourceConflictException AlreadyBacking()
        => new("You already have a pledge on this campaign. Change or cancel that pledge instead of making a second one.");

    private static MakePledgeResult ReplayOf(Pledge earlier, MakePledgeCommand command)
    {
        var sameRequest =
            earlier.CampaignId == command.CampaignId
            && earlier.Money.Amount == command.Amount
            && string.Equals(earlier.Money.Currency, command.Currency, StringComparison.OrdinalIgnoreCase)
            && earlier.RewardTierReservationId == command.RewardTierReservationId;

        if (!sameRequest)
        {
            throw new ResourceConflictException(
                "This idempotency key was already used for a different request. Use a new key for a new pledge.");
        }

        return new MakePledgeResult(earlier.Id, earlier.Status.ToString(), IsReplay: true);
    }

    private void EnsureCanPledge()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("The current user must be authenticated to pledge to a campaign.");
        }

        if (!_currentUser.HasPermission(PermissionConstants.CampaignsPledge))
        {
            throw new ForbiddenAccessException("The current user does not have permission to pledge to campaigns.");
        }
    }
}

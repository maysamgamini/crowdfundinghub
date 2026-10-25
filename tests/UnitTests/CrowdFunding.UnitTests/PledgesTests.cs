using CrowdFunding.BuildingBlocks.Application.Pagination;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Contracts.Enums;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCancelled;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignFailed;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Services;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.ConfirmPledgePayment;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.FailPledgePayment;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.MakePledge;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Events;
using CrowdFunding.Modules.Pledges.Infrastructure.Payments;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Queries.GetPledgeById;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Queries.ListPledgesByCampaign;
using CrowdFunding.Modules.Pledges.Domain.Aggregates;
using CrowdFunding.Modules.Pledges.Domain.Enums;
using CrowdFunding.Modules.Pledges.Domain.Events;
using CrowdFunding.Modules.Identity.Contracts.Authorization;

namespace CrowdFunding.UnitTests;

public sealed class PledgeDomainTests
{
    [Fact]
    public void Create_ShouldUseSharedMoneyValueObjectAndStartPending()
    {
        var createdAtUtc = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);

        var pledge = Pledge.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            25.555m,
            "usd",
            createdAtUtc);

        Assert.Equal(new Money(25.56m, "USD"), pledge.Money);
        Assert.Equal(PledgeStatus.Pending, pledge.Status);
        Assert.Equal(createdAtUtc, pledge.CreatedAtUtc);
        Assert.Empty(pledge.DomainEvents);
    }

    [Fact]
    public void Create_ShouldThrow_WhenAmountIsNotPositive()
    {
        var action = () => Pledge.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            0m,
            "USD",
            DateTime.UtcNow);

        var exception = Assert.Throws<ArgumentException>(action);

        Assert.Equal("Pledge amount must be greater than zero. (Parameter 'amount')", exception.Message);
    }

    [Fact]
    public void ConfirmPayment_ShouldMarkPledgeSucceeded_AndRaiseDomainEvent()
    {
        var pledge = Pledge.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            50m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        pledge.ConfirmPayment("PAY-123", new DateTime(2026, 4, 6, 12, 15, 0, DateTimeKind.Utc));

        Assert.Equal(PledgeStatus.Succeeded, pledge.Status);
        Assert.Equal("PAY-123", pledge.PaymentReference);
        Assert.Null(pledge.FailureReason);
        Assert.NotNull(pledge.ProcessedAtUtc);
        Assert.Contains(pledge.DomainEvents, domainEvent => domainEvent is PledgePaymentConfirmedDomainEvent);
    }

    [Fact]
    public void FailPayment_ShouldMarkPledgeFailed()
    {
        var pledge = Pledge.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            50m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        pledge.FailPayment("Card declined.", new DateTime(2026, 4, 6, 12, 15, 0, DateTimeKind.Utc));

        Assert.Equal(PledgeStatus.Failed, pledge.Status);
        Assert.Equal("Card declined.", pledge.FailureReason);
        Assert.Null(pledge.PaymentReference);
        Assert.NotNull(pledge.ProcessedAtUtc);
        Assert.Contains(pledge.DomainEvents, e => e is PledgePaymentFailedDomainEvent);
    }

    [Fact]
    public void FailPayment_WithRewardTierReservation_ShouldEmitDomainEventWithReservationId()
    {
        var reservationId = Guid.NewGuid();
        var pledge = Pledge.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            50m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc),
            rewardTierReservationId: reservationId);

        pledge.FailPayment("Card declined.", new DateTime(2026, 4, 6, 12, 15, 0, DateTimeKind.Utc));

        Assert.Equal(PledgeStatus.Failed, pledge.Status);
        var evt = Assert.Single(pledge.DomainEvents.OfType<PledgePaymentFailedDomainEvent>());
        Assert.Equal(reservationId, evt.RewardTierReservationId);
    }

    [Fact]
    public void Refund_WithRewardTierReservation_ShouldEmitDomainEventWithReservationId()
    {
        var reservationId = Guid.NewGuid();
        var pledge = Pledge.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            50m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc),
            rewardTierReservationId: reservationId);
        pledge.ConfirmPayment("REF-1", DateTime.UtcNow);

        pledge.Refund(DateTime.UtcNow);

        Assert.Equal(PledgeStatus.Refunded, pledge.Status);
        var evt = Assert.Single(pledge.DomainEvents.OfType<PledgeRefundedDomainEvent>());
        Assert.Equal(reservationId, evt.RewardTierReservationId);
    }

    [Fact]
    public void ConfirmPayment_ShouldThrow_WhenPledgeIsNotPending()
    {
        var pledge = Pledge.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            50m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));
        pledge.ConfirmPayment("PAY-123", new DateTime(2026, 4, 6, 12, 15, 0, DateTimeKind.Utc));

        var action = () => pledge.ConfirmPayment("PAY-456", new DateTime(2026, 4, 6, 12, 20, 0, DateTimeKind.Utc));

        var exception = Assert.Throws<InvalidOperationException>(action);

        Assert.Equal("Only pending pledges can be confirmed.", exception.Message);
    }

    [Fact]
    public void FailPayment_ShouldThrow_WhenReasonIsMissing()
    {
        var pledge = Pledge.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            50m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        var action = () => pledge.FailPayment("   ", new DateTime(2026, 4, 6, 12, 15, 0, DateTimeKind.Utc));

        var exception = Assert.Throws<ArgumentException>(action);

        Assert.Equal("Failure reason is required. (Parameter 'failureReason')", exception.Message);
    }

    [Fact]
    public void Create_ShouldThrow_WhenCampaignIdIsEmpty()
    {
        var action = () => Pledge.Create(
            Guid.Empty,
            Guid.NewGuid(),
            50m,
            "USD",
            DateTime.UtcNow);

        var exception = Assert.Throws<ArgumentException>(action);

        Assert.Equal("CampaignId is required. (Parameter 'campaignId')", exception.Message);
    }

    [Fact]
    public void Create_ShouldThrow_WhenBackerIdIsEmpty()
    {
        var action = () => Pledge.Create(
            Guid.NewGuid(),
            Guid.Empty,
            50m,
            "USD",
            DateTime.UtcNow);

        var exception = Assert.Throws<ArgumentException>(action);

        Assert.Equal("BackerId is required. (Parameter 'backerId')", exception.Message);
    }

    [Fact]
    public void ConfirmPayment_ShouldThrow_WhenPaymentReferenceIsMissing()
    {
        var pledge = Pledge.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            50m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        var action = () => pledge.ConfirmPayment("   ", new DateTime(2026, 4, 6, 12, 15, 0, DateTimeKind.Utc));

        var exception = Assert.Throws<ArgumentException>(action);

        Assert.Equal("Payment reference is required. (Parameter 'paymentReference')", exception.Message);
    }

    [Fact]
    public void FailPayment_ShouldThrow_WhenPledgeIsAlreadyCompleted()
    {
        var pledge = Pledge.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            50m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));
        pledge.ConfirmPayment("PAY-123", new DateTime(2026, 4, 6, 12, 15, 0, DateTimeKind.Utc));

        var action = () => pledge.FailPayment("Chargeback.", new DateTime(2026, 4, 6, 12, 20, 0, DateTimeKind.Utc));

        var exception = Assert.Throws<InvalidOperationException>(action);

        Assert.Equal("Only pending pledges can be failed.", exception.Message);
    }

    [Fact]
    public void FailPayment_ShouldMovePledgeToFailed_AndRaiseDomainEvent_WhenPending()
    {
        var reservationId = Guid.NewGuid();
        var pledge = Pledge.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            50m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc),
            rewardTierReservationId: reservationId);

        pledge.FailPayment("Card declined by issuer.", new DateTime(2026, 4, 6, 12, 5, 0, DateTimeKind.Utc));

        Assert.Equal(PledgeStatus.Failed, pledge.Status);
        Assert.Equal("Card declined by issuer.", pledge.FailureReason);
        var domainEvent = Assert.Single(pledge.DomainEvents.OfType<PledgePaymentFailedDomainEvent>());
        Assert.Equal(pledge.Id, domainEvent.PledgeId);
        Assert.Equal(reservationId, domainEvent.RewardTierReservationId);
        Assert.Equal("Card declined by issuer.", domainEvent.FailureReason);
    }

    [Fact]
    public void Refund_ShouldMovePledgeToRefunded_AndRaiseDomainEvent_WhenSucceeded()
    {
        var pledge = Pledge.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            50m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));
        pledge.ConfirmPayment("PAY-123", new DateTime(2026, 4, 6, 12, 15, 0, DateTimeKind.Utc));

        pledge.Refund(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(PledgeStatus.Refunded, pledge.Status);
        Assert.Contains(pledge.DomainEvents, domainEvent => domainEvent is PledgeRefundedDomainEvent);
    }

    [Fact]
    public void Refund_ShouldThrow_WhenPledgeIsNotSucceeded()
    {
        var pledge = Pledge.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            50m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        var action = () => pledge.Refund(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

        var exception = Assert.Throws<InvalidOperationException>(action);
        Assert.Equal("Cannot refund pledge with status 'Pending'.", exception.Message);
    }

    [Fact]
    public void Refund_ShouldThrow_WhenAlreadyRefunded()
    {
        var pledge = Pledge.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            50m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));
        pledge.ConfirmPayment("PAY-123", new DateTime(2026, 4, 6, 12, 15, 0, DateTimeKind.Utc));
        pledge.Refund(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

        var action = () => pledge.Refund(new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc));

        Assert.Throws<InvalidOperationException>(action);
    }
}

public sealed class MakePledgeCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldCreatePendingPledgeWithoutApplyingCampaignFunding()
    {
        var campaignReader = new FakeActiveCampaignCacheRepository(exists: true, isActive: true);
        var repository = new FakePledgeRepository();
        var currentUser = new TestCurrentUser
        {
            UserId = Guid.NewGuid(),
            Permissions = [PermissionConstants.CampaignsPledge]
        };
        var transactionExecutor = new FakePledgeTransactionExecutor();
        var handler = new MakePledgeCommandHandler(
            campaignReader,
            currentUser,
            new FakePledgeDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)),
            repository,
            transactionExecutor, new CrowdFunding.Modules.Pledges.Infrastructure.Payments.FakePaymentProvider(), new FakePaymentSettings());

        var command = new MakePledgeCommand(
            Guid.NewGuid(),
            100m,
            "usd");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.PledgeId);
        Assert.Equal("Pending", result.Status);
        Assert.NotNull(repository.SavedPledge);
        Assert.Equal(currentUser.UserId, repository.SavedPledge!.BackerId);
        Assert.Equal(PledgeStatus.Pending, repository.SavedPledge.Status);
        Assert.Equal(new Money(100m, "USD"), repository.SavedPledge.Money);
        Assert.Equal(result.PledgeId, repository.SavedPledge.Id);
        Assert.Equal(command.CampaignId, campaignReader.CheckedCampaignId);
        Assert.Equal(1, transactionExecutor.InvocationCount);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserIsNotAuthenticated()
    {
        var handler = new MakePledgeCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: true, isActive: true),
            new TestCurrentUser { IsAuthenticated = false, UserId = Guid.Empty },
            new FakePledgeDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)),
            new FakePledgeRepository(),
            new FakePledgeTransactionExecutor(), new CrowdFunding.Modules.Pledges.Infrastructure.Payments.FakePaymentProvider(), new FakePaymentSettings());

        var action = async () => await handler.Handle(
            new MakePledgeCommand(Guid.NewGuid(), 100m, "USD"),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(action);

        Assert.Equal("The current user must be authenticated to pledge to a campaign.", exception.Message);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserLacksPermission()
    {
        var handler = new MakePledgeCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: true, isActive: true),
            new TestCurrentUser(),
            new FakePledgeDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)),
            new FakePledgeRepository(),
            new FakePledgeTransactionExecutor(), new CrowdFunding.Modules.Pledges.Infrastructure.Payments.FakePaymentProvider(), new FakePaymentSettings());

        var action = async () => await handler.Handle(
            new MakePledgeCommand(Guid.NewGuid(), 100m, "USD"),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ForbiddenAccessException>(action);

        Assert.Equal("The current user does not have permission to pledge to campaigns.", exception.Message);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCampaignDoesNotExist()
    {
        var transactionExecutor = new FakePledgeTransactionExecutor();
        var campaignId = Guid.NewGuid();
        var handler = new MakePledgeCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: false, isActive: false),
            new TestCurrentUser
            {
                UserId = Guid.NewGuid(),
                Permissions = [PermissionConstants.CampaignsPledge]
            },
            new FakePledgeDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)),
            new FakePledgeRepository(),
            transactionExecutor, new CrowdFunding.Modules.Pledges.Infrastructure.Payments.FakePaymentProvider(), new FakePaymentSettings());

        var action = async () => await handler.Handle(
            new MakePledgeCommand(campaignId, 100m, "USD"),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(action);

        Assert.Equal($"Campaign with id '{campaignId}' was not found.", exception.Message);
        Assert.Equal(0, transactionExecutor.InvocationCount);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCampaignCannotAcceptPledges()
    {
        var campaignId = Guid.NewGuid();
        var transactionExecutor = new FakePledgeTransactionExecutor();
        var handler = new MakePledgeCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: true, isActive: false),
            new TestCurrentUser
            {
                UserId = Guid.NewGuid(),
                Permissions = [PermissionConstants.CampaignsPledge]
            },
            new FakePledgeDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)),
            new FakePledgeRepository(),
            transactionExecutor, new CrowdFunding.Modules.Pledges.Infrastructure.Payments.FakePaymentProvider(), new FakePaymentSettings());

        var action = async () => await handler.Handle(
            new MakePledgeCommand(campaignId, 100m, "USD"),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(action);

        Assert.Equal(
            $"Campaign '{campaignId}' cannot accept pledges — it is not active or has passed its deadline.",
            exception.Message);
        Assert.Equal(0, transactionExecutor.InvocationCount);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenPledgeCurrencyDoesNotMatchCampaignCurrency()
    {
        var campaignId = Guid.NewGuid();
        var transactionExecutor = new FakePledgeTransactionExecutor();
        var repository = new FakePledgeRepository();
        var handler = new MakePledgeCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: true, isActive: true, currency: "USD"),
            new TestCurrentUser
            {
                UserId = Guid.NewGuid(),
                Permissions = [PermissionConstants.CampaignsPledge]
            },
            new FakePledgeDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)),
            repository,
            transactionExecutor, new CrowdFunding.Modules.Pledges.Infrastructure.Payments.FakePaymentProvider(), new FakePaymentSettings());

        var action = async () => await handler.Handle(
            new MakePledgeCommand(campaignId, 100m, "EUR"),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(action);

        // Rejected here, before any Pledge/payment record is created — catching this at
        // MakePledge time instead of leaving it to surface as Money.Add throwing much
        // later (after payment confirmation) inside AddPledgeToCampaignCommandHandler,
        // which would permanently dead-letter the outbox message with no way to credit the
        // campaign without manual intervention.
        Assert.Equal("Pledge currency 'EUR' does not match campaign currency 'USD'.", exception.Message);
        Assert.Equal(0, transactionExecutor.InvocationCount);
        Assert.Null(repository.SavedPledge);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCreatorPledgesToOwnCampaign()
    {
        var campaignId = Guid.NewGuid();
        var creatorUserId = Guid.NewGuid();
        var transactionExecutor = new FakePledgeTransactionExecutor();
        var repository = new FakePledgeRepository();
        var handler = new MakePledgeCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: true, isActive: true, currency: "USD", ownerId: creatorUserId),
            new TestCurrentUser
            {
                UserId = creatorUserId,
                Permissions = [PermissionConstants.CampaignsPledge]
            },
            new FakePledgeDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)),
            repository,
            transactionExecutor, new CrowdFunding.Modules.Pledges.Infrastructure.Payments.FakePaymentProvider(), new FakePaymentSettings());

        var action = async () => await handler.Handle(
            new MakePledgeCommand(campaignId, 100m, "USD"),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(action);
        Assert.Equal("Campaign creators cannot back or pledge to their own campaigns.", exception.Message);
        Assert.Equal(0, transactionExecutor.InvocationCount);
        Assert.Null(repository.SavedPledge);
    }
}

public sealed class ConfirmPledgePaymentCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldPersistSucceededPledgeAndLeaveCampaignReactionToEvents()
    {
        var pledge = Pledge.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        var currentUser = new TestCurrentUser
        {
            Permissions = [PermissionConstants.PledgesPaymentsManage]
        };
        var campaignReader = new FakeActiveCampaignCacheRepository(exists: true, isActive: true);
        var repository = new FakePledgeRepository(pledge);
        var transactionExecutor = new FakePledgeTransactionExecutor();
        var handler = new ConfirmPledgePaymentCommandHandler(
            campaignReader,
            currentUser,
            new FakePledgeDateTimeProvider(new DateTime(2026, 4, 6, 12, 5, 0, DateTimeKind.Utc)),
            repository,
            transactionExecutor);

        var result = await handler.Handle(
            new ConfirmPledgePaymentCommand(pledge.CampaignId, pledge.Id, "PAY-123"),
            CancellationToken.None);

        Assert.Equal("Succeeded", result.Status);
        Assert.Equal("PAY-123", result.PaymentReference);
        Assert.True(repository.WasUpdated);
        Assert.Equal(PledgeStatus.Succeeded, pledge.Status);
        Assert.Equal(1, transactionExecutor.InvocationCount);
        Assert.Contains(pledge.DomainEvents, domainEvent => domainEvent is PledgePaymentConfirmedDomainEvent);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserIsNotAuthenticated()
    {
        var pledge = Pledge.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        var handler = new ConfirmPledgePaymentCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: true, isActive: true),
            new TestCurrentUser { IsAuthenticated = false, UserId = Guid.Empty },
            new FakePledgeDateTimeProvider(new DateTime(2026, 4, 6, 12, 5, 0, DateTimeKind.Utc)),
            new FakePledgeRepository(pledge),
            new FakePledgeTransactionExecutor());

        var action = async () => await handler.Handle(
            new ConfirmPledgePaymentCommand(pledge.CampaignId, pledge.Id, "PAY-123"),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(action);

        Assert.Equal("The current user must be authenticated to manage pledge payments.", exception.Message);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserLacksPermission()
    {
        var pledge = Pledge.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        var handler = new ConfirmPledgePaymentCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: true, isActive: true),
            new TestCurrentUser(),
            new FakePledgeDateTimeProvider(new DateTime(2026, 4, 6, 12, 5, 0, DateTimeKind.Utc)),
            new FakePledgeRepository(pledge),
            new FakePledgeTransactionExecutor());

        var action = async () => await handler.Handle(
            new ConfirmPledgePaymentCommand(pledge.CampaignId, pledge.Id, "PAY-123"),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ForbiddenAccessException>(action);

        Assert.Equal("The current user does not have permission to manage pledge payments.", exception.Message);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenPledgeDoesNotExist()
    {
        var transactionExecutor = new FakePledgeTransactionExecutor();
        var pledgeId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        var handler = new ConfirmPledgePaymentCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: true, isActive: true),
            new TestCurrentUser
            {
                Permissions = [PermissionConstants.PledgesPaymentsManage]
            },
            new FakePledgeDateTimeProvider(new DateTime(2026, 4, 6, 12, 5, 0, DateTimeKind.Utc)),
            new FakePledgeRepository(),
            transactionExecutor);

        var action = async () => await handler.Handle(
            new ConfirmPledgePaymentCommand(campaignId, pledgeId, "PAY-123"),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(action);

        Assert.Equal($"Pledge '{pledgeId}' was not found for campaign '{campaignId}'.", exception.Message);
        Assert.Equal(0, transactionExecutor.InvocationCount);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCampaignDoesNotExist()
    {
        var pledge = Pledge.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));
        var transactionExecutor = new FakePledgeTransactionExecutor();
        var handler = new ConfirmPledgePaymentCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: false, isActive: false),
            new TestCurrentUser
            {
                Permissions = [PermissionConstants.PledgesPaymentsManage]
            },
            new FakePledgeDateTimeProvider(new DateTime(2026, 4, 6, 12, 5, 0, DateTimeKind.Utc)),
            new FakePledgeRepository(pledge),
            transactionExecutor);

        var action = async () => await handler.Handle(
            new ConfirmPledgePaymentCommand(pledge.CampaignId, pledge.Id, "PAY-123"),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(action);

        Assert.Equal($"Campaign with id '{pledge.CampaignId}' was not found.", exception.Message);
        Assert.Equal(0, transactionExecutor.InvocationCount);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCampaignIsNotPublished()
    {
        var pledge = Pledge.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));
        var transactionExecutor = new FakePledgeTransactionExecutor();
        var handler = new ConfirmPledgePaymentCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: true, isActive: false),
            new TestCurrentUser
            {
                Permissions = [PermissionConstants.PledgesPaymentsManage]
            },
            new FakePledgeDateTimeProvider(new DateTime(2026, 4, 6, 12, 5, 0, DateTimeKind.Utc)),
            new FakePledgeRepository(pledge),
            transactionExecutor);

        var action = async () => await handler.Handle(
            new ConfirmPledgePaymentCommand(pledge.CampaignId, pledge.Id, "PAY-123"),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(action);

        Assert.Equal("Pledge payments can only be confirmed while the campaign is published.", exception.Message);
        Assert.Equal(0, transactionExecutor.InvocationCount);
    }
}

public sealed class FailPledgePaymentCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldPersistFailedPledge()
    {
        var pledge = Pledge.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        var currentUser = new TestCurrentUser
        {
            Permissions = [PermissionConstants.PledgesPaymentsManage]
        };
        var repository = new FakePledgeRepository(pledge);
        var transactionExecutor = new FakePledgeTransactionExecutor();
        var handler = new FailPledgePaymentCommandHandler(
            currentUser,
            new FakePledgeDateTimeProvider(new DateTime(2026, 4, 6, 12, 5, 0, DateTimeKind.Utc)),
            repository,
            transactionExecutor);

        var result = await handler.Handle(
            new FailPledgePaymentCommand(pledge.CampaignId, pledge.Id, "Card declined."),
            CancellationToken.None);

        Assert.Equal("Failed", result.Status);
        Assert.Equal("Card declined.", result.FailureReason);
        Assert.True(repository.WasUpdated);
        Assert.Equal(PledgeStatus.Failed, pledge.Status);
        Assert.Equal(1, transactionExecutor.InvocationCount);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserIsNotAuthenticated()
    {
        var pledge = Pledge.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        var handler = new FailPledgePaymentCommandHandler(
            new TestCurrentUser { IsAuthenticated = false, UserId = Guid.Empty },
            new FakePledgeDateTimeProvider(new DateTime(2026, 4, 6, 12, 5, 0, DateTimeKind.Utc)),
            new FakePledgeRepository(pledge),
            new FakePledgeTransactionExecutor());

        var action = async () => await handler.Handle(
            new FailPledgePaymentCommand(pledge.CampaignId, pledge.Id, "Card declined."),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(action);

        Assert.Equal("The current user must be authenticated to manage pledge payments.", exception.Message);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserLacksPermission()
    {
        var pledge = Pledge.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        var handler = new FailPledgePaymentCommandHandler(
            new TestCurrentUser(),
            new FakePledgeDateTimeProvider(new DateTime(2026, 4, 6, 12, 5, 0, DateTimeKind.Utc)),
            new FakePledgeRepository(pledge),
            new FakePledgeTransactionExecutor());

        var action = async () => await handler.Handle(
            new FailPledgePaymentCommand(pledge.CampaignId, pledge.Id, "Card declined."),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ForbiddenAccessException>(action);

        Assert.Equal("The current user does not have permission to manage pledge payments.", exception.Message);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenPledgeDoesNotExist()
    {
        var transactionExecutor = new FakePledgeTransactionExecutor();
        var pledgeId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        var handler = new FailPledgePaymentCommandHandler(
            new TestCurrentUser
            {
                Permissions = [PermissionConstants.PledgesPaymentsManage]
            },
            new FakePledgeDateTimeProvider(new DateTime(2026, 4, 6, 12, 5, 0, DateTimeKind.Utc)),
            new FakePledgeRepository(),
            transactionExecutor);

        var action = async () => await handler.Handle(
            new FailPledgePaymentCommand(campaignId, pledgeId, "Card declined."),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(action);

        Assert.Equal($"Pledge '{pledgeId}' was not found for campaign '{campaignId}'.", exception.Message);
        Assert.Equal(0, transactionExecutor.InvocationCount);
    }
}

public sealed class CampaignTerminationRefundHandlerTests
{
    private static (CampaignTerminationRefundHandler Handler, FakeRefundStore Store, FakePledgeTransactionExecutor Executor) Build()
    {
        var store = new FakeRefundStore([]);
        var executor = new FakePledgeTransactionExecutor();
        var handler = new CampaignTerminationRefundHandler(
            store, executor, new FakePledgeDateTimeProvider(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)));
        return (handler, store, executor);
    }

    [Fact]
    public async Task Handle_CampaignFailed_ShouldOpenARefundJobForTheCampaign()
    {
        var campaignId = Guid.NewGuid();
        var (handler, store, _) = Build();

        await handler.Handle(
            new CampaignFailedApplicationEvent(campaignId, Guid.NewGuid(), 300m, 1000m, "USD", DateTime.UtcNow),
            CancellationToken.None);

        var job = Assert.Single(store.Jobs);
        Assert.Equal(campaignId, job.CampaignId);
        Assert.Equal(RefundJobStatus.Open, job.Status);
    }

    [Fact]
    public async Task Handle_CampaignCancelled_ShouldOpenARefundJobForTheCampaign()
    {
        var campaignId = Guid.NewGuid();
        var (handler, store, _) = Build();

        await handler.Handle(new CampaignCancelledApplicationEvent(campaignId, Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(campaignId, Assert.Single(store.Jobs).CampaignId);
    }

    [Fact]
    public async Task Handle_ShouldBeIdempotent_WhenTheTriggeringEventIsRedelivered()
    {
        var campaignId = Guid.NewGuid();
        var (handler, store, executor) = Build();
        var @event = new CampaignFailedApplicationEvent(campaignId, Guid.NewGuid(), 150m, 1000m, "USD", DateTime.UtcNow);

        await handler.Handle(@event, CancellationToken.None);
        await handler.Handle(@event, CancellationToken.None);

        // The second delivery finds the job: no second job and no second transaction.
        Assert.Single(store.Jobs);
        Assert.Equal(1, executor.InvocationCount);
    }

    [Fact]
    public async Task Handle_ShouldNotRefundAnythingItself_TheJobDoesThat()
    {
        var campaignId = Guid.NewGuid();
        var pledge = Pledge.Create(campaignId, Guid.NewGuid(), 150m, "USD", DateTime.UtcNow);
        pledge.ConfirmPayment("PAY-1", DateTime.UtcNow);
        var (handler, _, _) = Build();

        await handler.Handle(new CampaignCancelledApplicationEvent(campaignId, Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(PledgeStatus.Succeeded, pledge.Status);
    }
}

internal sealed class FakeRefundablePledgeRepository : IPledgeRepository
{
    private readonly List<Pledge> _pledges;

    public FakeRefundablePledgeRepository(List<Pledge> pledges)
    {
        _pledges = pledges;
    }

    public Task AddActivationAsync(PledgeActivation activation, CancellationToken cancellationToken) => Task.CompletedTask;


    public Task AddAsync(Pledge pledge, CancellationToken cancellationToken)
        => throw new NotSupportedException("Not used by these tests.");

    public Task<Pledge?> GetByIdAsync(Guid pledgeId, CancellationToken cancellationToken)
        => Task.FromResult(_pledges.FirstOrDefault(x => x.Id == pledgeId));

    public Task<Pledge?> GetByExternalPaymentIntentIdAsync(string externalPaymentIntentId, CancellationToken cancellationToken)
        => Task.FromResult(_pledges.FirstOrDefault(x => x.ExternalPaymentIntentId == externalPaymentIntentId));

    public Task<Pledge?> GetByIdempotencyKeyAsync(Guid backerId, string idempotencyKey, CancellationToken cancellationToken)
        => Task.FromResult(_pledges.FirstOrDefault(x => x.BackerId == backerId && x.IdempotencyKey == idempotencyKey));

    public Task<Pledge?> GetLivePledgeAsync(Guid campaignId, Guid backerId, CancellationToken cancellationToken)
        => Task.FromResult(_pledges.FirstOrDefault(x => x.CampaignId == campaignId && x.BackerId == backerId && x.Status is PledgeStatus.Pending or PledgeStatus.Succeeded));

    public Task UpdateAsync(Pledge pledge, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task<IReadOnlyList<Pledge>> GetSucceededByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Pledge>>(_pledges
            .Where(x => x.CampaignId == campaignId && x.Status == PledgeStatus.Succeeded)
            .ToList());

    public Task<IReadOnlyList<Pledge>> GetSucceededBatchByCampaignIdAsync(Guid campaignId, int batchSize, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Pledge>>(_pledges
            .Where(x => x.CampaignId == campaignId && x.Status == PledgeStatus.Succeeded)
            .Take(batchSize)
            .ToList());
}

public sealed class ListPledgesByCampaignQueryHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnPledgeReadModels()
    {
        IReadOnlyCollection<ListPledgesByCampaignResult> pledges =
        [
            new(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                75m,
                "USD",
                "Succeeded",
                "PAY-123",
                null,
                new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 6, 12, 10, 0, DateTimeKind.Utc))
        ];

        var page = new PagedResult<ListPledgesByCampaignResult>(pledges, 1, 10, 1);
        var readService = new FakePledgeReadService(page);
        var handler = new ListPledgesByCampaignQueryHandler(readService, new TestCurrentUser { UserId = Guid.NewGuid(), Permissions = [PermissionConstants.PledgesPaymentsManage] }, new FakeActiveCampaignCacheRepository(exists: true, isActive: true));
        var filter = new ListPledgesByCampaignFilter(Guid.NewGuid(), "USD", "Succeeded");

        var result = await handler.Handle(
            new ListPledgesByCampaignQuery(
                pledges.Single().CampaignId,
                new PageRequest(1, 10),
                filter),
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(75m, result.Items.Single().Amount);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(new PageRequest(1, 10), readService.ReceivedPageRequest);
        Assert.Equal(filter, readService.ReceivedFilter);
    }
}

public sealed class GetPledgeByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnPledge_WhenFound()
    {
        var campaignId = Guid.NewGuid();
        var pledgeId = Guid.NewGuid();
        IReadOnlyCollection<ListPledgesByCampaignResult> pledges =
        [
            new(
                pledgeId,
                campaignId,
                Guid.NewGuid(),
                75m,
                "USD",
                "Succeeded",
                "PAY-123",
                null,
                new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 6, 12, 10, 0, DateTimeKind.Utc))
        ];
        var readService = new FakePledgeReadService(new PagedResult<ListPledgesByCampaignResult>(pledges, 1, 10, 1));
        var handler = new GetPledgeByIdQueryHandler(readService, new TestCurrentUser { UserId = Guid.NewGuid(), Permissions = [PermissionConstants.PledgesPaymentsManage] }, new FakeActiveCampaignCacheRepository(exists: true, isActive: true));

        var result = await handler.Handle(new GetPledgeByIdQuery(campaignId, pledgeId), CancellationToken.None);

        Assert.Equal(pledgeId, result.Id);
        Assert.Equal(75m, result.Amount);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenPledgeWasNotFound()
    {
        var readService = new FakePledgeReadService(new PagedResult<ListPledgesByCampaignResult>([], 1, 10, 0));
        var handler = new GetPledgeByIdQueryHandler(readService, new TestCurrentUser { UserId = Guid.NewGuid(), Permissions = [PermissionConstants.PledgesPaymentsManage] }, new FakeActiveCampaignCacheRepository(exists: true, isActive: true));
        var campaignId = Guid.NewGuid();
        var pledgeId = Guid.NewGuid();

        var action = async () => await handler.Handle(new GetPledgeByIdQuery(campaignId, pledgeId), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(action);
        Assert.Equal($"Pledge '{pledgeId}' was not found for campaign '{campaignId}'.", exception.Message);
    }
}

public sealed class MakePledgeCommandValidatorTests
{
    [Fact]
    public void Validate_ShouldReturnErrors_WhenCommandIsInvalid()
    {
        var validator = new MakePledgeCommandValidator();
        var result = validator.Validate(new MakePledgeCommand(Guid.Empty, 0m, "US"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(MakePledgeCommand.CampaignId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(MakePledgeCommand.Amount));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(MakePledgeCommand.Currency));
    }
}

public sealed class ConfirmPledgePaymentCommandValidatorTests
{
    [Fact]
    public void Validate_ShouldReturnErrors_WhenCommandIsInvalid()
    {
        var validator = new ConfirmPledgePaymentCommandValidator();
        var result = validator.Validate(new ConfirmPledgePaymentCommand(Guid.Empty, Guid.Empty, ""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ConfirmPledgePaymentCommand.CampaignId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ConfirmPledgePaymentCommand.PledgeId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ConfirmPledgePaymentCommand.PaymentReference));
    }
}

public sealed class FailPledgePaymentCommandValidatorTests
{
    [Fact]
    public void Validate_ShouldReturnErrors_WhenCommandIsInvalid()
    {
        var validator = new FailPledgePaymentCommandValidator();
        var result = validator.Validate(new FailPledgePaymentCommand(Guid.Empty, Guid.Empty, ""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(FailPledgePaymentCommand.CampaignId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(FailPledgePaymentCommand.PledgeId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(FailPledgePaymentCommand.FailureReason));
    }
}

internal sealed class FakePledgeRepository : IPledgeRepository
{
    private readonly Pledge? _pledge;

    public FakePledgeRepository(Pledge? pledge = null)
    {
        _pledge = pledge;
    }

    public Pledge? SavedPledge { get; private set; }
    public bool WasUpdated { get; private set; }

    public Task AddActivationAsync(PledgeActivation activation, CancellationToken cancellationToken) => Task.CompletedTask;


    public Task AddAsync(Pledge pledge, CancellationToken cancellationToken)
    {
        SavedPledge = pledge;
        return Task.CompletedTask;
    }

    public Task<Pledge?> GetLivePledgeAsync(Guid campaignId, Guid backerId, CancellationToken cancellationToken)
    {
        var match = new[] { _pledge, SavedPledge }
            .FirstOrDefault(x => x is not null && x.CampaignId == campaignId && x.BackerId == backerId && x.Status is PledgeStatus.Pending or PledgeStatus.Succeeded);
        return Task.FromResult(match);
    }

    public Task<Pledge?> GetByIdempotencyKeyAsync(Guid backerId, string idempotencyKey, CancellationToken cancellationToken)
    {
        var match = new[] { _pledge, SavedPledge }
            .FirstOrDefault(x => x is not null && x.BackerId == backerId && x.IdempotencyKey == idempotencyKey);
        return Task.FromResult(match);
    }

    public Task<Pledge?> GetByIdAsync(Guid pledgeId, CancellationToken cancellationToken)
    {
        if (_pledge?.Id == pledgeId)
        {
            return Task.FromResult<Pledge?>(_pledge);
        }

        return Task.FromResult(SavedPledge?.Id == pledgeId ? SavedPledge : null);
    }

    public Task<Pledge?> GetByExternalPaymentIntentIdAsync(string externalPaymentIntentId, CancellationToken cancellationToken)
    {
        if (_pledge?.ExternalPaymentIntentId == externalPaymentIntentId)
        {
            return Task.FromResult<Pledge?>(_pledge);
        }

        return Task.FromResult(SavedPledge?.ExternalPaymentIntentId == externalPaymentIntentId ? SavedPledge : null);
    }

    public Task UpdateAsync(Pledge pledge, CancellationToken cancellationToken)
    {
        WasUpdated = true;
        SavedPledge = pledge;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Pledge>> GetSucceededByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken)
        => throw new NotSupportedException("Not used by these tests.");

    public Task<IReadOnlyList<Pledge>> GetSucceededBatchByCampaignIdAsync(Guid campaignId, int batchSize, CancellationToken cancellationToken)
        => throw new NotSupportedException("Not used by these tests.");
}

internal sealed class FakeActiveCampaignCacheRepository : IActiveCampaignCacheRepository
{
    private readonly bool _exists;
    private readonly bool _isActive;
    private readonly string _currency;
    private readonly DateTime _deadlineUtc;
    private readonly Guid _ownerId;

    public FakeActiveCampaignCacheRepository(bool exists, bool isActive, string currency = "USD", DateTime? deadlineUtc = null, Guid? ownerId = null)
    {
        _exists = exists;
        _isActive = isActive;
        _currency = currency;
        _deadlineUtc = deadlineUtc ?? DateTime.MaxValue.AddDays(-1);
        _ownerId = ownerId ?? Guid.NewGuid();
    }

    public Guid? CheckedCampaignId { get; private set; }

    public Task<ActiveCampaignSnapshot?> GetAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        CheckedCampaignId = campaignId;
        return Task.FromResult(_exists
            ? new ActiveCampaignSnapshot(campaignId, _ownerId, _currency, _isActive, _deadlineUtc)
            : null);
    }

    public Task UpsertAsync(Guid campaignId, Guid ownerId, string title, string currency, bool isActive, DateTime deadlineUtc, DateTime updatedAtUtc, CancellationToken cancellationToken)
        => throw new NotSupportedException("Not used by these tests.");

    public Task SetActiveStatusAsync(Guid campaignId, bool isActive, DateTime updatedAtUtc, CancellationToken cancellationToken)
        => throw new NotSupportedException("Not used by these tests.");
}

internal sealed class FakePledgeDateTimeProvider : IPledgeDateTimeProvider
{
    public FakePledgeDateTimeProvider(DateTime utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTime UtcNow { get; }
}

internal sealed class FakePledgeReadService : IPledgeReadService
{
    private readonly PagedResult<ListPledgesByCampaignResult> _pledgesPage;

    public FakePledgeReadService(PagedResult<ListPledgesByCampaignResult> pledgesPage)
    {
        _pledgesPage = pledgesPage;
    }

    public PageRequest? ReceivedPageRequest { get; private set; }

    public ListPledgesByCampaignFilter? ReceivedFilter { get; private set; }

    public Task<PagedResult<ListPledgesByCampaignResult>> ListByCampaignAsync(
        Guid campaignId,
        PageRequest pageRequest,
        ListPledgesByCampaignFilter filter,
        CancellationToken cancellationToken)
    {
        ReceivedPageRequest = pageRequest;
        ReceivedFilter = filter;
        return Task.FromResult(_pledgesPage);
    }

    public Task<GetPledgeByIdResult?> GetByIdAsync(Guid campaignId, Guid pledgeId, CancellationToken cancellationToken)
    {
        var match = _pledgesPage.Items.FirstOrDefault(x => x.Id == pledgeId && x.CampaignId == campaignId);

        return Task.FromResult(match is null
            ? null
            : new GetPledgeByIdResult(
                match.Id,
                match.CampaignId,
                match.BackerId,
                match.Amount,
                match.Currency,
                match.Status,
                match.PaymentReference,
                match.FailureReason,
                match.CreatedAtUtc,
                match.ProcessedAtUtc,
                ExternalPaymentIntentId: null));
    }
}

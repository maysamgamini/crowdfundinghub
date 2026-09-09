using CrowdFunding.BuildingBlocks.Application.Pagination;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Contracts.Enums;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCancelled;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignFailed;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Services;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.ConfirmContributionPayment;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.FailContributionPayment;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.MakeContribution;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Events;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Queries.GetContributionById;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Queries.ListContributionsByCampaign;
using CrowdFunding.Modules.Contributions.Domain.Aggregates;
using CrowdFunding.Modules.Contributions.Domain.Enums;
using CrowdFunding.Modules.Contributions.Domain.Events;
using CrowdFunding.Modules.Identity.Contracts.Authorization;

namespace CrowdFunding.UnitTests;

public sealed class ContributionDomainTests
{
    [Fact]
    public void Create_ShouldUseSharedMoneyValueObjectAndStartPending()
    {
        var createdAtUtc = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);

        var contribution = Contribution.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            25.555m,
            "usd",
            createdAtUtc);

        Assert.Equal(new Money(25.56m, "USD"), contribution.Money);
        Assert.Equal(ContributionStatus.Pending, contribution.Status);
        Assert.Equal(createdAtUtc, contribution.CreatedAtUtc);
        Assert.Empty(contribution.DomainEvents);
    }

    [Fact]
    public void Create_ShouldThrow_WhenAmountIsNotPositive()
    {
        var action = () => Contribution.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            0m,
            "USD",
            DateTime.UtcNow);

        var exception = Assert.Throws<ArgumentException>(action);

        Assert.Equal("Contribution amount must be greater than zero. (Parameter 'amount')", exception.Message);
    }

    [Fact]
    public void ConfirmPayment_ShouldMarkContributionSucceeded_AndRaiseDomainEvent()
    {
        var contribution = Contribution.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            50m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        contribution.ConfirmPayment("PAY-123", new DateTime(2026, 4, 6, 12, 15, 0, DateTimeKind.Utc));

        Assert.Equal(ContributionStatus.Succeeded, contribution.Status);
        Assert.Equal("PAY-123", contribution.PaymentReference);
        Assert.Null(contribution.FailureReason);
        Assert.NotNull(contribution.ProcessedAtUtc);
        Assert.Contains(contribution.DomainEvents, domainEvent => domainEvent is ContributionPaymentConfirmedDomainEvent);
    }

    [Fact]
    public void FailPayment_ShouldMarkContributionFailed()
    {
        var contribution = Contribution.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            50m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        contribution.FailPayment("Card declined.", new DateTime(2026, 4, 6, 12, 15, 0, DateTimeKind.Utc));

        Assert.Equal(ContributionStatus.Failed, contribution.Status);
        Assert.Equal("Card declined.", contribution.FailureReason);
        Assert.Null(contribution.PaymentReference);
        Assert.NotNull(contribution.ProcessedAtUtc);
    }

    [Fact]
    public void ConfirmPayment_ShouldThrow_WhenContributionIsNotPending()
    {
        var contribution = Contribution.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            50m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));
        contribution.ConfirmPayment("PAY-123", new DateTime(2026, 4, 6, 12, 15, 0, DateTimeKind.Utc));

        var action = () => contribution.ConfirmPayment("PAY-456", new DateTime(2026, 4, 6, 12, 20, 0, DateTimeKind.Utc));

        var exception = Assert.Throws<InvalidOperationException>(action);

        Assert.Equal("Only pending contributions can be confirmed.", exception.Message);
    }

    [Fact]
    public void FailPayment_ShouldThrow_WhenReasonIsMissing()
    {
        var contribution = Contribution.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            50m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        var action = () => contribution.FailPayment("   ", new DateTime(2026, 4, 6, 12, 15, 0, DateTimeKind.Utc));

        var exception = Assert.Throws<ArgumentException>(action);

        Assert.Equal("Failure reason is required. (Parameter 'failureReason')", exception.Message);
    }

    [Fact]
    public void Create_ShouldThrow_WhenCampaignIdIsEmpty()
    {
        var action = () => Contribution.Create(
            Guid.Empty,
            Guid.NewGuid(),
            50m,
            "USD",
            DateTime.UtcNow);

        var exception = Assert.Throws<ArgumentException>(action);

        Assert.Equal("CampaignId is required. (Parameter 'campaignId')", exception.Message);
    }

    [Fact]
    public void Create_ShouldThrow_WhenContributorIdIsEmpty()
    {
        var action = () => Contribution.Create(
            Guid.NewGuid(),
            Guid.Empty,
            50m,
            "USD",
            DateTime.UtcNow);

        var exception = Assert.Throws<ArgumentException>(action);

        Assert.Equal("ContributorId is required. (Parameter 'contributorId')", exception.Message);
    }

    [Fact]
    public void ConfirmPayment_ShouldThrow_WhenPaymentReferenceIsMissing()
    {
        var contribution = Contribution.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            50m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        var action = () => contribution.ConfirmPayment("   ", new DateTime(2026, 4, 6, 12, 15, 0, DateTimeKind.Utc));

        var exception = Assert.Throws<ArgumentException>(action);

        Assert.Equal("Payment reference is required. (Parameter 'paymentReference')", exception.Message);
    }

    [Fact]
    public void FailPayment_ShouldThrow_WhenContributionIsAlreadyCompleted()
    {
        var contribution = Contribution.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            50m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));
        contribution.ConfirmPayment("PAY-123", new DateTime(2026, 4, 6, 12, 15, 0, DateTimeKind.Utc));

        var action = () => contribution.FailPayment("Chargeback.", new DateTime(2026, 4, 6, 12, 20, 0, DateTimeKind.Utc));

        var exception = Assert.Throws<InvalidOperationException>(action);

        Assert.Equal("Only pending contributions can be failed.", exception.Message);
    }

    [Fact]
    public void FailPayment_ShouldMoveContributionToFailed_AndRaiseDomainEvent_WhenPending()
    {
        var reservationId = Guid.NewGuid();
        var contribution = Contribution.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            50m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc),
            rewardTierReservationId: reservationId);

        contribution.FailPayment("Card declined by issuer.", new DateTime(2026, 4, 6, 12, 5, 0, DateTimeKind.Utc));

        Assert.Equal(ContributionStatus.Failed, contribution.Status);
        Assert.Equal("Card declined by issuer.", contribution.FailureReason);
        var domainEvent = Assert.Single(contribution.DomainEvents.OfType<ContributionPaymentFailedDomainEvent>());
        Assert.Equal(contribution.Id, domainEvent.ContributionId);
        Assert.Equal(reservationId, domainEvent.RewardTierReservationId);
        Assert.Equal("Card declined by issuer.", domainEvent.FailureReason);
    }

    [Fact]
    public void Refund_ShouldMoveContributionToRefunded_AndRaiseDomainEvent_WhenSucceeded()
    {
        var contribution = Contribution.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            50m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));
        contribution.ConfirmPayment("PAY-123", new DateTime(2026, 4, 6, 12, 15, 0, DateTimeKind.Utc));

        contribution.Refund(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(ContributionStatus.Refunded, contribution.Status);
        Assert.Contains(contribution.DomainEvents, domainEvent => domainEvent is ContributionRefundedDomainEvent);
    }

    [Fact]
    public void Refund_ShouldThrow_WhenContributionIsNotSucceeded()
    {
        var contribution = Contribution.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            50m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        var action = () => contribution.Refund(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

        var exception = Assert.Throws<InvalidOperationException>(action);
        Assert.Equal("Cannot refund contribution with status 'Pending'.", exception.Message);
    }

    [Fact]
    public void Refund_ShouldThrow_WhenAlreadyRefunded()
    {
        var contribution = Contribution.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            50m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));
        contribution.ConfirmPayment("PAY-123", new DateTime(2026, 4, 6, 12, 15, 0, DateTimeKind.Utc));
        contribution.Refund(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

        var action = () => contribution.Refund(new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc));

        Assert.Throws<InvalidOperationException>(action);
    }
}

public sealed class MakeContributionCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldCreatePendingContributionWithoutApplyingCampaignFunding()
    {
        var campaignReader = new FakeActiveCampaignCacheRepository(exists: true, isActive: true);
        var repository = new FakeContributionRepository();
        var currentUser = new TestCurrentUser
        {
            UserId = Guid.NewGuid(),
            Permissions = [PermissionConstants.CampaignsContribute]
        };
        var transactionExecutor = new FakeContributionTransactionExecutor();
        var handler = new MakeContributionCommandHandler(
            campaignReader,
            currentUser,
            new FakeContributionDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)),
            repository,
            transactionExecutor);

        var command = new MakeContributionCommand(
            Guid.NewGuid(),
            100m,
            "usd");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.ContributionId);
        Assert.Equal("Pending", result.Status);
        Assert.NotNull(repository.SavedContribution);
        Assert.Equal(currentUser.UserId, repository.SavedContribution!.ContributorId);
        Assert.Equal(ContributionStatus.Pending, repository.SavedContribution.Status);
        Assert.Equal(new Money(100m, "USD"), repository.SavedContribution.Money);
        Assert.Equal(result.ContributionId, repository.SavedContribution.Id);
        Assert.Equal(command.CampaignId, campaignReader.CheckedCampaignId);
        Assert.Equal(1, transactionExecutor.InvocationCount);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserIsNotAuthenticated()
    {
        var handler = new MakeContributionCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: true, isActive: true),
            new TestCurrentUser { IsAuthenticated = false, UserId = Guid.Empty },
            new FakeContributionDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)),
            new FakeContributionRepository(),
            new FakeContributionTransactionExecutor());

        var action = async () => await handler.Handle(
            new MakeContributionCommand(Guid.NewGuid(), 100m, "USD"),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(action);

        Assert.Equal("The current user must be authenticated to contribute to a campaign.", exception.Message);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserLacksPermission()
    {
        var handler = new MakeContributionCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: true, isActive: true),
            new TestCurrentUser(),
            new FakeContributionDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)),
            new FakeContributionRepository(),
            new FakeContributionTransactionExecutor());

        var action = async () => await handler.Handle(
            new MakeContributionCommand(Guid.NewGuid(), 100m, "USD"),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ForbiddenAccessException>(action);

        Assert.Equal("The current user does not have permission to contribute to campaigns.", exception.Message);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCampaignDoesNotExist()
    {
        var transactionExecutor = new FakeContributionTransactionExecutor();
        var campaignId = Guid.NewGuid();
        var handler = new MakeContributionCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: false, isActive: false),
            new TestCurrentUser
            {
                UserId = Guid.NewGuid(),
                Permissions = [PermissionConstants.CampaignsContribute]
            },
            new FakeContributionDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)),
            new FakeContributionRepository(),
            transactionExecutor);

        var action = async () => await handler.Handle(
            new MakeContributionCommand(campaignId, 100m, "USD"),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(action);

        Assert.Equal($"Campaign with id '{campaignId}' was not found.", exception.Message);
        Assert.Equal(0, transactionExecutor.InvocationCount);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCampaignCannotAcceptContributions()
    {
        var campaignId = Guid.NewGuid();
        var transactionExecutor = new FakeContributionTransactionExecutor();
        var handler = new MakeContributionCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: true, isActive: false),
            new TestCurrentUser
            {
                UserId = Guid.NewGuid(),
                Permissions = [PermissionConstants.CampaignsContribute]
            },
            new FakeContributionDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)),
            new FakeContributionRepository(),
            transactionExecutor);

        var action = async () => await handler.Handle(
            new MakeContributionCommand(campaignId, 100m, "USD"),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(action);

        Assert.Equal(
            $"Campaign '{campaignId}' cannot accept contributions — it is not active or has passed its deadline.",
            exception.Message);
        Assert.Equal(0, transactionExecutor.InvocationCount);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenContributionCurrencyDoesNotMatchCampaignCurrency()
    {
        var campaignId = Guid.NewGuid();
        var transactionExecutor = new FakeContributionTransactionExecutor();
        var repository = new FakeContributionRepository();
        var handler = new MakeContributionCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: true, isActive: true, currency: "USD"),
            new TestCurrentUser
            {
                UserId = Guid.NewGuid(),
                Permissions = [PermissionConstants.CampaignsContribute]
            },
            new FakeContributionDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)),
            repository,
            transactionExecutor);

        var action = async () => await handler.Handle(
            new MakeContributionCommand(campaignId, 100m, "EUR"),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(action);

        // Rejected here, before any Contribution/payment record is created — catching this at
        // MakeContribution time instead of leaving it to surface as Money.Add throwing much
        // later (after payment confirmation) inside AddContributionToCampaignCommandHandler,
        // which would permanently dead-letter the outbox message with no way to credit the
        // campaign without manual intervention.
        Assert.Equal("Contribution currency 'EUR' does not match campaign currency 'USD'.", exception.Message);
        Assert.Equal(0, transactionExecutor.InvocationCount);
        Assert.Null(repository.SavedContribution);
    }
}

public sealed class ConfirmContributionPaymentCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldPersistSucceededContributionAndLeaveCampaignReactionToEvents()
    {
        var contribution = Contribution.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        var currentUser = new TestCurrentUser
        {
            Permissions = [PermissionConstants.ContributionsPaymentsManage]
        };
        var campaignReader = new FakeActiveCampaignCacheRepository(exists: true, isActive: true);
        var repository = new FakeContributionRepository(contribution);
        var transactionExecutor = new FakeContributionTransactionExecutor();
        var handler = new ConfirmContributionPaymentCommandHandler(
            campaignReader,
            currentUser,
            new FakeContributionDateTimeProvider(new DateTime(2026, 4, 6, 12, 5, 0, DateTimeKind.Utc)),
            repository,
            transactionExecutor);

        var result = await handler.Handle(
            new ConfirmContributionPaymentCommand(contribution.CampaignId, contribution.Id, "PAY-123"),
            CancellationToken.None);

        Assert.Equal("Succeeded", result.Status);
        Assert.Equal("PAY-123", result.PaymentReference);
        Assert.True(repository.WasUpdated);
        Assert.Equal(ContributionStatus.Succeeded, contribution.Status);
        Assert.Equal(1, transactionExecutor.InvocationCount);
        Assert.Contains(contribution.DomainEvents, domainEvent => domainEvent is ContributionPaymentConfirmedDomainEvent);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserIsNotAuthenticated()
    {
        var contribution = Contribution.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        var handler = new ConfirmContributionPaymentCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: true, isActive: true),
            new TestCurrentUser { IsAuthenticated = false, UserId = Guid.Empty },
            new FakeContributionDateTimeProvider(new DateTime(2026, 4, 6, 12, 5, 0, DateTimeKind.Utc)),
            new FakeContributionRepository(contribution),
            new FakeContributionTransactionExecutor());

        var action = async () => await handler.Handle(
            new ConfirmContributionPaymentCommand(contribution.CampaignId, contribution.Id, "PAY-123"),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(action);

        Assert.Equal("The current user must be authenticated to manage contribution payments.", exception.Message);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserLacksPermission()
    {
        var contribution = Contribution.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        var handler = new ConfirmContributionPaymentCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: true, isActive: true),
            new TestCurrentUser(),
            new FakeContributionDateTimeProvider(new DateTime(2026, 4, 6, 12, 5, 0, DateTimeKind.Utc)),
            new FakeContributionRepository(contribution),
            new FakeContributionTransactionExecutor());

        var action = async () => await handler.Handle(
            new ConfirmContributionPaymentCommand(contribution.CampaignId, contribution.Id, "PAY-123"),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ForbiddenAccessException>(action);

        Assert.Equal("The current user does not have permission to manage contribution payments.", exception.Message);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenContributionDoesNotExist()
    {
        var transactionExecutor = new FakeContributionTransactionExecutor();
        var contributionId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        var handler = new ConfirmContributionPaymentCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: true, isActive: true),
            new TestCurrentUser
            {
                Permissions = [PermissionConstants.ContributionsPaymentsManage]
            },
            new FakeContributionDateTimeProvider(new DateTime(2026, 4, 6, 12, 5, 0, DateTimeKind.Utc)),
            new FakeContributionRepository(),
            transactionExecutor);

        var action = async () => await handler.Handle(
            new ConfirmContributionPaymentCommand(campaignId, contributionId, "PAY-123"),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(action);

        Assert.Equal($"Contribution '{contributionId}' was not found for campaign '{campaignId}'.", exception.Message);
        Assert.Equal(0, transactionExecutor.InvocationCount);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCampaignDoesNotExist()
    {
        var contribution = Contribution.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));
        var transactionExecutor = new FakeContributionTransactionExecutor();
        var handler = new ConfirmContributionPaymentCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: false, isActive: false),
            new TestCurrentUser
            {
                Permissions = [PermissionConstants.ContributionsPaymentsManage]
            },
            new FakeContributionDateTimeProvider(new DateTime(2026, 4, 6, 12, 5, 0, DateTimeKind.Utc)),
            new FakeContributionRepository(contribution),
            transactionExecutor);

        var action = async () => await handler.Handle(
            new ConfirmContributionPaymentCommand(contribution.CampaignId, contribution.Id, "PAY-123"),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(action);

        Assert.Equal($"Campaign with id '{contribution.CampaignId}' was not found.", exception.Message);
        Assert.Equal(0, transactionExecutor.InvocationCount);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCampaignIsNotPublished()
    {
        var contribution = Contribution.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));
        var transactionExecutor = new FakeContributionTransactionExecutor();
        var handler = new ConfirmContributionPaymentCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: true, isActive: false),
            new TestCurrentUser
            {
                Permissions = [PermissionConstants.ContributionsPaymentsManage]
            },
            new FakeContributionDateTimeProvider(new DateTime(2026, 4, 6, 12, 5, 0, DateTimeKind.Utc)),
            new FakeContributionRepository(contribution),
            transactionExecutor);

        var action = async () => await handler.Handle(
            new ConfirmContributionPaymentCommand(contribution.CampaignId, contribution.Id, "PAY-123"),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(action);

        Assert.Equal("Contribution payments can only be confirmed while the campaign is published.", exception.Message);
        Assert.Equal(0, transactionExecutor.InvocationCount);
    }
}

public sealed class FailContributionPaymentCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldPersistFailedContribution()
    {
        var contribution = Contribution.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        var currentUser = new TestCurrentUser
        {
            Permissions = [PermissionConstants.ContributionsPaymentsManage]
        };
        var repository = new FakeContributionRepository(contribution);
        var transactionExecutor = new FakeContributionTransactionExecutor();
        var handler = new FailContributionPaymentCommandHandler(
            currentUser,
            new FakeContributionDateTimeProvider(new DateTime(2026, 4, 6, 12, 5, 0, DateTimeKind.Utc)),
            repository,
            transactionExecutor);

        var result = await handler.Handle(
            new FailContributionPaymentCommand(contribution.CampaignId, contribution.Id, "Card declined."),
            CancellationToken.None);

        Assert.Equal("Failed", result.Status);
        Assert.Equal("Card declined.", result.FailureReason);
        Assert.True(repository.WasUpdated);
        Assert.Equal(ContributionStatus.Failed, contribution.Status);
        Assert.Equal(1, transactionExecutor.InvocationCount);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserIsNotAuthenticated()
    {
        var contribution = Contribution.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        var handler = new FailContributionPaymentCommandHandler(
            new TestCurrentUser { IsAuthenticated = false, UserId = Guid.Empty },
            new FakeContributionDateTimeProvider(new DateTime(2026, 4, 6, 12, 5, 0, DateTimeKind.Utc)),
            new FakeContributionRepository(contribution),
            new FakeContributionTransactionExecutor());

        var action = async () => await handler.Handle(
            new FailContributionPaymentCommand(contribution.CampaignId, contribution.Id, "Card declined."),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(action);

        Assert.Equal("The current user must be authenticated to manage contribution payments.", exception.Message);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserLacksPermission()
    {
        var contribution = Contribution.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        var handler = new FailContributionPaymentCommandHandler(
            new TestCurrentUser(),
            new FakeContributionDateTimeProvider(new DateTime(2026, 4, 6, 12, 5, 0, DateTimeKind.Utc)),
            new FakeContributionRepository(contribution),
            new FakeContributionTransactionExecutor());

        var action = async () => await handler.Handle(
            new FailContributionPaymentCommand(contribution.CampaignId, contribution.Id, "Card declined."),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ForbiddenAccessException>(action);

        Assert.Equal("The current user does not have permission to manage contribution payments.", exception.Message);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenContributionDoesNotExist()
    {
        var transactionExecutor = new FakeContributionTransactionExecutor();
        var contributionId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        var handler = new FailContributionPaymentCommandHandler(
            new TestCurrentUser
            {
                Permissions = [PermissionConstants.ContributionsPaymentsManage]
            },
            new FakeContributionDateTimeProvider(new DateTime(2026, 4, 6, 12, 5, 0, DateTimeKind.Utc)),
            new FakeContributionRepository(),
            transactionExecutor);

        var action = async () => await handler.Handle(
            new FailContributionPaymentCommand(campaignId, contributionId, "Card declined."),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(action);

        Assert.Equal($"Contribution '{contributionId}' was not found for campaign '{campaignId}'.", exception.Message);
        Assert.Equal(0, transactionExecutor.InvocationCount);
    }
}

public sealed class CampaignTerminationRefundHandlerTests
{
    [Fact]
    public async Task Handle_CampaignFailed_ShouldRefundEverySucceededContribution()
    {
        var campaignId = Guid.NewGuid();
        var contributionOne = Contribution.Create(campaignId, Guid.NewGuid(), 100m, "USD", DateTime.UtcNow);
        contributionOne.ConfirmPayment("PAY-1", DateTime.UtcNow);
        var contributionTwo = Contribution.Create(campaignId, Guid.NewGuid(), 200m, "USD", DateTime.UtcNow);
        contributionTwo.ConfirmPayment("PAY-2", DateTime.UtcNow);
        var repository = new FakeRefundableContributionRepository([contributionOne, contributionTwo]);
        var transactionExecutor = new FakeContributionTransactionExecutor();
        var handler = new CampaignTerminationRefundHandler(
            repository,
            transactionExecutor,
            new FakeContributionDateTimeProvider(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)));

        await handler.Handle(
            new CampaignFailedApplicationEvent(campaignId, Guid.NewGuid(), 300m, 1000m, "USD", DateTime.UtcNow),
            CancellationToken.None);

        Assert.Equal(ContributionStatus.Refunded, contributionOne.Status);
        Assert.Equal(ContributionStatus.Refunded, contributionTwo.Status);
        Assert.Equal(1, transactionExecutor.InvocationCount);
    }

    [Fact]
    public async Task Handle_CampaignCancelled_ShouldRefundEverySucceededContribution()
    {
        var campaignId = Guid.NewGuid();
        var contribution = Contribution.Create(campaignId, Guid.NewGuid(), 150m, "USD", DateTime.UtcNow);
        contribution.ConfirmPayment("PAY-1", DateTime.UtcNow);
        var repository = new FakeRefundableContributionRepository([contribution]);
        var transactionExecutor = new FakeContributionTransactionExecutor();
        var handler = new CampaignTerminationRefundHandler(
            repository,
            transactionExecutor,
            new FakeContributionDateTimeProvider(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)));

        await handler.Handle(new CampaignCancelledApplicationEvent(campaignId, Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(ContributionStatus.Refunded, contribution.Status);
    }

    [Fact]
    public async Task Handle_ShouldBeIdempotent_WhenTheTriggeringEventIsRedelivered()
    {
        var campaignId = Guid.NewGuid();
        var contribution = Contribution.Create(campaignId, Guid.NewGuid(), 150m, "USD", DateTime.UtcNow);
        contribution.ConfirmPayment("PAY-1", DateTime.UtcNow);
        var repository = new FakeRefundableContributionRepository([contribution]);
        var transactionExecutor = new FakeContributionTransactionExecutor();
        var handler = new CampaignTerminationRefundHandler(
            repository,
            transactionExecutor,
            new FakeContributionDateTimeProvider(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)));
        var @event = new CampaignFailedApplicationEvent(campaignId, Guid.NewGuid(), 150m, 1000m, "USD", DateTime.UtcNow);

        await handler.Handle(@event, CancellationToken.None);
        await handler.Handle(@event, CancellationToken.None);

        // GetSucceededByCampaignIdAsync only ever returns Succeeded rows, so the second delivery
        // finds nothing left to refund — no exception, no double-refund, no second transaction.
        Assert.Equal(ContributionStatus.Refunded, contribution.Status);
        Assert.Equal(1, transactionExecutor.InvocationCount);
    }
}

internal sealed class FakeRefundableContributionRepository : IContributionRepository
{
    private readonly List<Contribution> _contributions;

    public FakeRefundableContributionRepository(List<Contribution> contributions)
    {
        _contributions = contributions;
    }

    public Task AddAsync(Contribution contribution, CancellationToken cancellationToken)
        => throw new NotSupportedException("Not used by these tests.");

    public Task<Contribution?> GetByIdAsync(Guid contributionId, CancellationToken cancellationToken)
        => Task.FromResult(_contributions.FirstOrDefault(x => x.Id == contributionId));

    public Task<Contribution?> GetByExternalPaymentIntentIdAsync(string externalPaymentIntentId, CancellationToken cancellationToken)
        => Task.FromResult(_contributions.FirstOrDefault(x => x.ExternalPaymentIntentId == externalPaymentIntentId));

    public Task UpdateAsync(Contribution contribution, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task<IReadOnlyList<Contribution>> GetSucceededByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Contribution>>(_contributions
            .Where(x => x.CampaignId == campaignId && x.Status == ContributionStatus.Succeeded)
            .ToList());
}

public sealed class ListContributionsByCampaignQueryHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnContributionReadModels()
    {
        IReadOnlyCollection<ListContributionsByCampaignResult> contributions =
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

        var page = new PagedResult<ListContributionsByCampaignResult>(contributions, 1, 10, 1);
        var readService = new FakeContributionReadService(page);
        var handler = new ListContributionsByCampaignQueryHandler(readService);
        var filter = new ListContributionsByCampaignFilter(Guid.NewGuid(), "USD", "Succeeded");

        var result = await handler.Handle(
            new ListContributionsByCampaignQuery(
                contributions.Single().CampaignId,
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

public sealed class GetContributionByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnContribution_WhenFound()
    {
        var campaignId = Guid.NewGuid();
        var contributionId = Guid.NewGuid();
        IReadOnlyCollection<ListContributionsByCampaignResult> contributions =
        [
            new(
                contributionId,
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
        var readService = new FakeContributionReadService(new PagedResult<ListContributionsByCampaignResult>(contributions, 1, 10, 1));
        var handler = new GetContributionByIdQueryHandler(readService);

        var result = await handler.Handle(new GetContributionByIdQuery(campaignId, contributionId), CancellationToken.None);

        Assert.Equal(contributionId, result.Id);
        Assert.Equal(75m, result.Amount);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenContributionWasNotFound()
    {
        var readService = new FakeContributionReadService(new PagedResult<ListContributionsByCampaignResult>([], 1, 10, 0));
        var handler = new GetContributionByIdQueryHandler(readService);
        var campaignId = Guid.NewGuid();
        var contributionId = Guid.NewGuid();

        var action = async () => await handler.Handle(new GetContributionByIdQuery(campaignId, contributionId), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(action);
        Assert.Equal($"Contribution '{contributionId}' was not found for campaign '{campaignId}'.", exception.Message);
    }
}

public sealed class MakeContributionCommandValidatorTests
{
    [Fact]
    public void Validate_ShouldReturnErrors_WhenCommandIsInvalid()
    {
        var validator = new MakeContributionCommandValidator();
        var result = validator.Validate(new MakeContributionCommand(Guid.Empty, 0m, "US"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(MakeContributionCommand.CampaignId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(MakeContributionCommand.Amount));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(MakeContributionCommand.Currency));
    }
}

public sealed class ConfirmContributionPaymentCommandValidatorTests
{
    [Fact]
    public void Validate_ShouldReturnErrors_WhenCommandIsInvalid()
    {
        var validator = new ConfirmContributionPaymentCommandValidator();
        var result = validator.Validate(new ConfirmContributionPaymentCommand(Guid.Empty, Guid.Empty, ""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ConfirmContributionPaymentCommand.CampaignId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ConfirmContributionPaymentCommand.ContributionId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ConfirmContributionPaymentCommand.PaymentReference));
    }
}

public sealed class FailContributionPaymentCommandValidatorTests
{
    [Fact]
    public void Validate_ShouldReturnErrors_WhenCommandIsInvalid()
    {
        var validator = new FailContributionPaymentCommandValidator();
        var result = validator.Validate(new FailContributionPaymentCommand(Guid.Empty, Guid.Empty, ""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(FailContributionPaymentCommand.CampaignId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(FailContributionPaymentCommand.ContributionId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(FailContributionPaymentCommand.FailureReason));
    }
}

internal sealed class FakeContributionRepository : IContributionRepository
{
    private readonly Contribution? _contribution;

    public FakeContributionRepository(Contribution? contribution = null)
    {
        _contribution = contribution;
    }

    public Contribution? SavedContribution { get; private set; }
    public bool WasUpdated { get; private set; }

    public Task AddAsync(Contribution contribution, CancellationToken cancellationToken)
    {
        SavedContribution = contribution;
        return Task.CompletedTask;
    }

    public Task<Contribution?> GetByIdAsync(Guid contributionId, CancellationToken cancellationToken)
    {
        if (_contribution?.Id == contributionId)
        {
            return Task.FromResult<Contribution?>(_contribution);
        }

        return Task.FromResult(SavedContribution?.Id == contributionId ? SavedContribution : null);
    }

    public Task<Contribution?> GetByExternalPaymentIntentIdAsync(string externalPaymentIntentId, CancellationToken cancellationToken)
    {
        if (_contribution?.ExternalPaymentIntentId == externalPaymentIntentId)
        {
            return Task.FromResult<Contribution?>(_contribution);
        }

        return Task.FromResult(SavedContribution?.ExternalPaymentIntentId == externalPaymentIntentId ? SavedContribution : null);
    }

    public Task UpdateAsync(Contribution contribution, CancellationToken cancellationToken)
    {
        WasUpdated = true;
        SavedContribution = contribution;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Contribution>> GetSucceededByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken)
        => throw new NotSupportedException("Not used by these tests.");
}

internal sealed class FakeActiveCampaignCacheRepository : IActiveCampaignCacheRepository
{
    private readonly bool _exists;
    private readonly bool _isActive;
    private readonly string _currency;
    private readonly DateTime _deadlineUtc;

    public FakeActiveCampaignCacheRepository(bool exists, bool isActive, string currency = "USD", DateTime? deadlineUtc = null)
    {
        _exists = exists;
        _isActive = isActive;
        _currency = currency;
        _deadlineUtc = deadlineUtc ?? DateTime.MaxValue.AddDays(-1);
    }

    public Guid? CheckedCampaignId { get; private set; }

    public Task<ActiveCampaignSnapshot?> GetAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        CheckedCampaignId = campaignId;
        return Task.FromResult(_exists
            ? new ActiveCampaignSnapshot(campaignId, _currency, _isActive, _deadlineUtc)
            : null);
    }

    public Task UpsertAsync(Guid campaignId, string title, string currency, bool isActive, DateTime deadlineUtc, DateTime updatedAtUtc, CancellationToken cancellationToken)
        => throw new NotSupportedException("Not used by these tests.");

    public Task SetActiveStatusAsync(Guid campaignId, bool isActive, DateTime updatedAtUtc, CancellationToken cancellationToken)
        => throw new NotSupportedException("Not used by these tests.");
}

internal sealed class FakeContributionDateTimeProvider : IContributionDateTimeProvider
{
    public FakeContributionDateTimeProvider(DateTime utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTime UtcNow { get; }
}

internal sealed class FakeContributionReadService : IContributionReadService
{
    private readonly PagedResult<ListContributionsByCampaignResult> _contributionsPage;

    public FakeContributionReadService(PagedResult<ListContributionsByCampaignResult> contributionsPage)
    {
        _contributionsPage = contributionsPage;
    }

    public PageRequest? ReceivedPageRequest { get; private set; }

    public ListContributionsByCampaignFilter? ReceivedFilter { get; private set; }

    public Task<PagedResult<ListContributionsByCampaignResult>> ListByCampaignAsync(
        Guid campaignId,
        PageRequest pageRequest,
        ListContributionsByCampaignFilter filter,
        CancellationToken cancellationToken)
    {
        ReceivedPageRequest = pageRequest;
        ReceivedFilter = filter;
        return Task.FromResult(_contributionsPage);
    }

    public Task<GetContributionByIdResult?> GetByIdAsync(Guid campaignId, Guid contributionId, CancellationToken cancellationToken)
    {
        var match = _contributionsPage.Items.FirstOrDefault(x => x.Id == contributionId && x.CampaignId == campaignId);

        return Task.FromResult(match is null
            ? null
            : new GetContributionByIdResult(
                match.Id,
                match.CampaignId,
                match.ContributorId,
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

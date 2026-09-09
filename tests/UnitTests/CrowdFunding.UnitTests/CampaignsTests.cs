using CrowdFunding.BuildingBlocks.Application.Exceptions;
using CrowdFunding.BuildingBlocks.Application.Pagination;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CancelCampaign;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CreateCampaign;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.PublishCampaign;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Events;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.GetCampaignById;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.ListCampaigns;
using CrowdFunding.Modules.Campaigns.Contracts.Commands.AddContributionToCampaign;
using CrowdFunding.Modules.Campaigns.Contracts.Enums;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Campaigns.Domain.Enums;
using CrowdFunding.Modules.Campaigns.Domain.Events;
using CrowdFunding.Modules.Contributions.Contracts.Events.ContributionPaymentConfirmed;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using CrowdFunding.Modules.Moderation.Contracts.Enums;
using CrowdFunding.Modules.Moderation.Contracts.Queries.GetCampaignReviewStatusByCampaignId;

namespace CrowdFunding.UnitTests;

public sealed class CampaignDomainTests
{
    [Fact]
    public void Create_ShouldInitializeDraftCampaignWithZeroRaisedAmount_AndRaiseDomainEvent()
    {
        var createdAtUtc = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);

        var campaign = Campaign.Create(
            Guid.NewGuid(),
            "Launch a clean architecture sample",
            "We are building a modular clean architecture sample project.",
            "Technology",
            new Money(5000m, "usd"),
            createdAtUtc.AddDays(30),
            createdAtUtc);

        Assert.Equal(CampaignStatus.Draft, campaign.Status);
        Assert.Equal(new Money(0m, "USD"), campaign.RaisedAmount);
        Assert.Equal(new Money(5000m, "USD"), campaign.GoalAmount);
        Assert.Contains(campaign.DomainEvents, domainEvent => domainEvent is CampaignCreatedDomainEvent);
    }

    [Fact]
    public void Publish_ShouldMoveDraftCampaignToPublished_AndRaiseDomainEvent()
    {
        var createdAtUtc = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);
        var campaign = CreateDraftCampaign(createdAtUtc, createdAtUtc.AddDays(14));

        campaign.Publish(createdAtUtc.AddDays(1));

        Assert.Equal(CampaignStatus.Published, campaign.Status);
        Assert.Contains(campaign.DomainEvents, domainEvent => domainEvent is CampaignPublishedDomainEvent);
    }

    [Fact]
    public void Publish_ShouldThrow_WhenDeadlineHasAlreadyPassed()
    {
        var createdAtUtc = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);
        var deadlineUtc = createdAtUtc.AddDays(2);
        var campaign = CreateDraftCampaign(createdAtUtc, deadlineUtc);

        var action = () => campaign.Publish(deadlineUtc);

        var exception = Assert.Throws<InvalidOperationException>(action);

        Assert.Equal("Cannot publish a campaign with a past deadline.", exception.Message);
    }

    [Fact]
    public void Cancel_ShouldMoveCampaignToCancelled_AndRaiseDomainEvent()
    {
        var createdAtUtc = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);
        var campaign = CreateDraftCampaign(createdAtUtc, createdAtUtc.AddDays(14));

        campaign.Cancel();

        Assert.Equal(CampaignStatus.Cancelled, campaign.Status);
        Assert.Contains(campaign.DomainEvents, domainEvent => domainEvent is CampaignCancelledDomainEvent);
    }

    [Fact]
    public void Create_ShouldThrow_WhenStoryIsTooShort()
    {
        var action = () => Campaign.Create(
            Guid.NewGuid(),
            "Short story campaign",
            "Too short.",
            "Community",
            new Money(100m, "USD"),
            DateTime.UtcNow.AddDays(5),
            DateTime.UtcNow);

        var exception = Assert.Throws<ArgumentException>(action);

        Assert.Equal("Campaign story must be at least 20 characters. (Parameter 'story')", exception.Message);
    }

    [Fact]
    public void Create_ShouldThrow_WhenOwnerIdIsEmpty()
    {
        var action = () => Campaign.Create(
            Guid.Empty,
            "Valid title",
            "A story that is definitely longer than twenty characters.",
            "Community",
            new Money(100m, "USD"),
            DateTime.UtcNow.AddDays(5),
            DateTime.UtcNow);

        var exception = Assert.Throws<ArgumentException>(action);

        Assert.Equal("OwnerId is required. (Parameter 'ownerId')", exception.Message);
    }

    [Fact]
    public void Create_ShouldThrow_WhenTitleIsBlank()
    {
        var action = () => Campaign.Create(
            Guid.NewGuid(),
            "   ",
            "A story that is definitely longer than twenty characters.",
            "Community",
            new Money(100m, "USD"),
            DateTime.UtcNow.AddDays(5),
            DateTime.UtcNow);

        var exception = Assert.Throws<ArgumentException>(action);

        Assert.Equal("Campaign title is required. (Parameter 'title')", exception.Message);
    }

    [Fact]
    public void Create_ShouldThrow_WhenTitleExceedsMaximumLength()
    {
        var action = () => Campaign.Create(
            Guid.NewGuid(),
            new string('a', 201),
            "A story that is definitely longer than twenty characters.",
            "Community",
            new Money(100m, "USD"),
            DateTime.UtcNow.AddDays(5),
            DateTime.UtcNow);

        var exception = Assert.Throws<ArgumentException>(action);

        Assert.Equal("Campaign title cannot exceed 200 characters. (Parameter 'title')", exception.Message);
    }

    [Fact]
    public void Create_ShouldThrow_WhenCategoryIsBlank()
    {
        var action = () => Campaign.Create(
            Guid.NewGuid(),
            "Valid title",
            "A story that is definitely longer than twenty characters.",
            "   ",
            new Money(100m, "USD"),
            DateTime.UtcNow.AddDays(5),
            DateTime.UtcNow);

        var exception = Assert.Throws<ArgumentException>(action);

        Assert.Equal("Campaign category is required. (Parameter 'category')", exception.Message);
    }

    [Fact]
    public void Create_ShouldThrow_WhenCategoryExceedsMaximumLength()
    {
        var action = () => Campaign.Create(
            Guid.NewGuid(),
            "Valid title",
            "A story that is definitely longer than twenty characters.",
            new string('a', 101),
            new Money(100m, "USD"),
            DateTime.UtcNow.AddDays(5),
            DateTime.UtcNow);

        var exception = Assert.Throws<ArgumentException>(action);

        Assert.Equal("Campaign category cannot exceed 100 characters. (Parameter 'category')", exception.Message);
    }

    [Fact]
    public void Create_ShouldThrow_WhenGoalAmountIsNotPositive()
    {
        var action = () => Campaign.Create(
            Guid.NewGuid(),
            "Valid title",
            "A story that is definitely longer than twenty characters.",
            "Community",
            new Money(0m, "USD"),
            DateTime.UtcNow.AddDays(5),
            DateTime.UtcNow);

        var exception = Assert.Throws<ArgumentException>(action);

        Assert.Equal("Campaign goal amount must be greater than zero. (Parameter 'goalAmount')", exception.Message);
    }

    [Fact]
    public void Create_ShouldThrow_WhenDeadlineIsNotAfterCreatedDate()
    {
        var createdAtUtc = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);

        var action = () => Campaign.Create(
            Guid.NewGuid(),
            "Valid title",
            "A story that is definitely longer than twenty characters.",
            "Community",
            new Money(100m, "USD"),
            createdAtUtc,
            createdAtUtc);

        var exception = Assert.Throws<ArgumentException>(action);

        Assert.Equal("Campaign deadline must be in the future. (Parameter 'deadlineUtc')", exception.Message);
    }

    [Fact]
    public void Publish_ShouldThrow_WhenCampaignIsNotDraft()
    {
        var createdAtUtc = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);
        var campaign = CreateDraftCampaign(createdAtUtc, createdAtUtc.AddDays(14));
        campaign.Publish(createdAtUtc.AddDays(1));

        var action = () => campaign.Publish(createdAtUtc.AddDays(2));

        var exception = Assert.Throws<InvalidOperationException>(action);

        Assert.Equal("Only draft campaigns can be published.", exception.Message);
    }

    [Fact]
    public void Cancel_ShouldThrow_WhenCampaignIsAlreadyCancelled()
    {
        var createdAtUtc = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);
        var campaign = CreateDraftCampaign(createdAtUtc, createdAtUtc.AddDays(14));
        campaign.Cancel();

        var action = () => campaign.Cancel();

        var exception = Assert.Throws<InvalidOperationException>(action);

        // Previously this succeeded silently and re-raised a duplicate
        // CampaignCancelledDomainEvent on every repeated call.
        Assert.Equal("Campaign is already cancelled.", exception.Message);
    }

    [Fact]
    public void ApplyConfirmedContribution_ShouldIncreaseRaisedAmount_WhenCampaignIsPublished()
    {
        var createdAtUtc = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);
        var campaign = CreateDraftCampaign(createdAtUtc, createdAtUtc.AddDays(14));
        campaign.Publish(createdAtUtc.AddDays(1));

        campaign.ApplyConfirmedContribution(new Money(125m, "usd"));

        Assert.Equal(new Money(125m, "USD"), campaign.RaisedAmount);
    }

    [Fact]
    public void ApplyConfirmedContribution_ShouldThrow_WhenCampaignIsDraft()
    {
        var createdAtUtc = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);
        var campaign = CreateDraftCampaign(createdAtUtc, createdAtUtc.AddDays(14));

        var action = () => campaign.ApplyConfirmedContribution(new Money(125m, "USD"));

        var exception = Assert.Throws<InvalidOperationException>(action);

        Assert.Equal(
            "Cannot apply contributions to campaign in status 'Draft'. Only Published campaigns can accept funds.",
            exception.Message);
    }

    [Fact]
    public void ApplyConfirmedContribution_ShouldThrow_WhenCampaignIsCancelled()
    {
        var createdAtUtc = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);
        var campaign = CreateDraftCampaign(createdAtUtc, createdAtUtc.AddDays(14));
        campaign.Publish(createdAtUtc.AddDays(1));
        campaign.Cancel();

        var action = () => campaign.ApplyConfirmedContribution(new Money(125m, "USD"));

        var exception = Assert.Throws<InvalidOperationException>(action);

        Assert.Equal(
            "Cannot apply contributions to campaign in status 'Cancelled'. Only Published campaigns can accept funds.",
            exception.Message);
    }

    [Fact]
    public void ApplyConfirmedContribution_ShouldThrow_WhenAmountIsNotPositive()
    {
        var createdAtUtc = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);
        var campaign = CreateDraftCampaign(createdAtUtc, createdAtUtc.AddDays(14));
        campaign.Publish(createdAtUtc.AddDays(1));

        var action = () => campaign.ApplyConfirmedContribution(new Money(0m, "USD"));

        var exception = Assert.Throws<InvalidOperationException>(action);

        Assert.Equal("Contribution amount must be greater than zero.", exception.Message);
    }

    [Fact]
    public void CompleteSuccessfully_ShouldMoveCampaignToSuccessful_WhenGoalWasReached()
    {
        var createdAtUtc = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);
        var campaign = CreateDraftCampaign(createdAtUtc, createdAtUtc.AddDays(14));
        campaign.Publish(createdAtUtc.AddDays(1));
        campaign.ApplyConfirmedContribution(new Money(10000m, "USD"));

        campaign.CompleteSuccessfully(createdAtUtc.AddDays(15));

        Assert.Equal(CampaignStatus.Successful, campaign.Status);
        Assert.Contains(campaign.DomainEvents, domainEvent => domainEvent is CampaignSucceededDomainEvent);
    }

    [Fact]
    public void CompleteSuccessfully_ShouldThrow_WhenGoalWasNotReached()
    {
        var createdAtUtc = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);
        var campaign = CreateDraftCampaign(createdAtUtc, createdAtUtc.AddDays(14));
        campaign.Publish(createdAtUtc.AddDays(1));
        campaign.ApplyConfirmedContribution(new Money(500m, "USD"));

        var action = () => campaign.CompleteSuccessfully(createdAtUtc.AddDays(15));

        var exception = Assert.Throws<InvalidOperationException>(action);
        Assert.Equal("Cannot complete a campaign successfully — its funding goal was not reached.", exception.Message);
    }

    [Fact]
    public void CompleteSuccessfully_ShouldThrow_WhenCampaignIsNotPublished()
    {
        var createdAtUtc = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);
        var campaign = CreateDraftCampaign(createdAtUtc, createdAtUtc.AddDays(14));

        var action = () => campaign.CompleteSuccessfully(createdAtUtc.AddDays(15));

        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void MarkFailed_ShouldMoveCampaignToFailed_WhenGoalWasNotReached()
    {
        var createdAtUtc = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);
        var campaign = CreateDraftCampaign(createdAtUtc, createdAtUtc.AddDays(14));
        campaign.Publish(createdAtUtc.AddDays(1));
        campaign.ApplyConfirmedContribution(new Money(500m, "USD"));

        campaign.MarkFailed(createdAtUtc.AddDays(15));

        Assert.Equal(CampaignStatus.Failed, campaign.Status);
        Assert.Contains(campaign.DomainEvents, domainEvent => domainEvent is CampaignFailedDomainEvent);
    }

    [Fact]
    public void MarkFailed_ShouldThrow_WhenGoalWasReached()
    {
        var createdAtUtc = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);
        var campaign = CreateDraftCampaign(createdAtUtc, createdAtUtc.AddDays(14));
        campaign.Publish(createdAtUtc.AddDays(1));
        campaign.ApplyConfirmedContribution(new Money(10000m, "USD"));

        var action = () => campaign.MarkFailed(createdAtUtc.AddDays(15));

        var exception = Assert.Throws<InvalidOperationException>(action);
        Assert.Equal("Cannot mark a campaign failed — its funding goal was reached.", exception.Message);
    }

    private static Campaign CreateDraftCampaign(DateTime createdAtUtc, DateTime deadlineUtc)
    {
        return Campaign.Create(
            Guid.NewGuid(),
            "Save the community library",
            "This campaign helps renovate and reopen the local community library.",
            "Community",
            new Money(10000m, "USD"),
            deadlineUtc,
            createdAtUtc);
    }
}

public sealed class CreateCampaignCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldCreateCampaignInsideTransaction()
    {
        var repository = new FakeCampaignRepository();
        var transactionExecutor = new FakeCampaignTransactionExecutor();
        var currentUser = new TestCurrentUser
        {
            UserId = Guid.NewGuid(),
            Permissions = [PermissionConstants.CampaignsCreate]
        };
        var handler = new CreateCampaignCommandHandler(
            repository,
            currentUser,
            new FakeDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)),
            transactionExecutor);

        var command = new CreateCampaignCommand(
            "Build a neighborhood makerspace",
            "We are raising funds to create a neighborhood makerspace for students and families.",
            "Community",
            15000m,
            "USD",
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(repository.SavedCampaign);
        Assert.Equal(result.CampaignId, repository.SavedCampaign!.Id);
        Assert.Equal(currentUser.UserId, repository.SavedCampaign.OwnerId);
        Assert.Equal(1, transactionExecutor.InvocationCount);
        Assert.Contains(repository.SavedCampaign.DomainEvents, domainEvent => domainEvent is CampaignCreatedDomainEvent);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserIsNotAuthenticated()
    {
        var handler = new CreateCampaignCommandHandler(
            new FakeCampaignRepository(),
            new TestCurrentUser { IsAuthenticated = false, UserId = Guid.Empty },
            new FakeDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)),
            new FakeCampaignTransactionExecutor());

        var action = async () => await handler.Handle(
            new CreateCampaignCommand(
                "Build a neighborhood makerspace",
                "We are raising funds to create a neighborhood makerspace for students and families.",
                "Community",
                15000m,
                "USD",
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(action);

        Assert.Equal("The current user must be authenticated to create a campaign.", exception.Message);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserLacksPermission()
    {
        var handler = new CreateCampaignCommandHandler(
            new FakeCampaignRepository(),
            new TestCurrentUser(),
            new FakeDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)),
            new FakeCampaignTransactionExecutor());

        var action = async () => await handler.Handle(
            new CreateCampaignCommand(
                "Build a neighborhood makerspace",
                "We are raising funds to create a neighborhood makerspace for students and families.",
                "Community",
                15000m,
                "USD",
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ForbiddenAccessException>(action);

        Assert.Equal("The current user does not have permission to create campaigns.", exception.Message);
    }
}

public sealed class PublishCampaignCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldPublishCampaignAndPersistChanges()
    {
        var now = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);
        var ownerId = Guid.NewGuid();
        var campaign = Campaign.Create(
            ownerId,
            "Fund a neighborhood garden",
            "This campaign builds a shared neighborhood garden for local families.",
            "Community",
            new Money(2500m, "USD"),
            now.AddDays(10),
            now.AddDays(-1));

        var repository = new FakeCampaignRepository(campaign);
        var reviewStatusReader = new FakeCampaignReviewStatusReader(CampaignReviewStatusContract.Approved);
        var transactionExecutor = new FakeCampaignTransactionExecutor();
        var handler = new PublishCampaignCommandHandler(
            repository,
            new TestCurrentUser
            {
                UserId = ownerId,
                Permissions = [PermissionConstants.CampaignsPublish]
            },
            new FakeDateTimeProvider(now),
            reviewStatusReader,
            transactionExecutor);

        var result = await handler.Handle(new PublishCampaignCommand(campaign.Id), CancellationToken.None);

        Assert.Equal(campaign.Id, result.CampaignId);
        Assert.Equal("Published", result.Status);
        Assert.Equal(CampaignStatus.Published, campaign.Status);
        Assert.True(repository.WasUpdated);
        Assert.Equal(campaign.Id, reviewStatusReader.CheckedCampaignId);
        Assert.Equal(1, transactionExecutor.InvocationCount);
        Assert.Contains(campaign.DomainEvents, domainEvent => domainEvent is CampaignPublishedDomainEvent);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCampaignDoesNotExist()
    {
        var handler = new PublishCampaignCommandHandler(
            new FakeCampaignRepository(),
            new TestCurrentUser { UserId = Guid.NewGuid() },
            new FakeDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)),
            new FakeCampaignReviewStatusReader(CampaignReviewStatusContract.Approved),
            new FakeCampaignTransactionExecutor());

        var action = async () => await handler.Handle(new PublishCampaignCommand(Guid.NewGuid()), CancellationToken.None);

        await Assert.ThrowsAsync<KeyNotFoundException>(action);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserDoesNotOwnCampaign()
    {
        var now = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);
        var campaign = Campaign.Create(
            Guid.NewGuid(),
            "Fund a neighborhood garden",
            "This campaign builds a shared neighborhood garden for local families.",
            "Community",
            new Money(2500m, "USD"),
            now.AddDays(10),
            now.AddDays(-1));

        var handler = new PublishCampaignCommandHandler(
            new FakeCampaignRepository(campaign),
            new TestCurrentUser { UserId = Guid.NewGuid() },
            new FakeDateTimeProvider(now),
            new FakeCampaignReviewStatusReader(CampaignReviewStatusContract.Approved),
            new FakeCampaignTransactionExecutor());

        var action = async () => await handler.Handle(new PublishCampaignCommand(campaign.Id), CancellationToken.None);

        await Assert.ThrowsAsync<ForbiddenAccessException>(action);
    }

    [Fact]
    public async Task Handle_ShouldAllowAdministratorToPublishCampaignTheyDoNotOwn()
    {
        var now = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);
        var campaign = Campaign.Create(
            Guid.NewGuid(),
            "Fund a neighborhood garden",
            "This campaign builds a shared neighborhood garden for local families.",
            "Community",
            new Money(2500m, "USD"),
            now.AddDays(10),
            now.AddDays(-1));

        var repository = new FakeCampaignRepository(campaign);
        var transactionExecutor = new FakeCampaignTransactionExecutor();
        var handler = new PublishCampaignCommandHandler(
            repository,
            new TestCurrentUser
            {
                UserId = Guid.NewGuid(),
                Permissions = [PermissionConstants.CampaignsManageAny]
            },
            new FakeDateTimeProvider(now),
            new FakeCampaignReviewStatusReader(CampaignReviewStatusContract.Approved),
            transactionExecutor);

        var result = await handler.Handle(new PublishCampaignCommand(campaign.Id), CancellationToken.None);

        Assert.Equal("Published", result.Status);
        Assert.Equal(CampaignStatus.Published, campaign.Status);
        Assert.True(repository.WasUpdated);
        Assert.Equal(1, transactionExecutor.InvocationCount);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenReviewIsNotApproved()
    {
        var now = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);
        var ownerId = Guid.NewGuid();
        var campaign = Campaign.Create(
            ownerId,
            "Fund a neighborhood garden",
            "This campaign builds a shared neighborhood garden for local families.",
            "Community",
            new Money(2500m, "USD"),
            now.AddDays(10),
            now.AddDays(-1));

        var transactionExecutor = new FakeCampaignTransactionExecutor();
        var handler = new PublishCampaignCommandHandler(
            new FakeCampaignRepository(campaign),
            new TestCurrentUser
            {
                UserId = ownerId,
                Permissions = [PermissionConstants.CampaignsPublish]
            },
            new FakeDateTimeProvider(now),
            new FakeCampaignReviewStatusReader(CampaignReviewStatusContract.Pending),
            transactionExecutor);

        var action = async () => await handler.Handle(new PublishCampaignCommand(campaign.Id), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ResourceConflictException>(action);

        Assert.Equal("Campaign must be approved by moderation before it can be published.", exception.Message);
        // TICKET-041: the moderation status is now re-verified inside the advisory-locked
        // transaction (closing the TOCTOU window against a concurrent CancelCampaign), so the
        // executor IS invoked — it just rolls back without updating the campaign.
        Assert.Equal(1, transactionExecutor.InvocationCount);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserIsNotAuthenticated()
    {
        var now = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);
        var campaign = Campaign.Create(
            Guid.NewGuid(),
            "Fund a neighborhood garden",
            "This campaign builds a shared neighborhood garden for local families.",
            "Community",
            new Money(2500m, "USD"),
            now.AddDays(10),
            now.AddDays(-1));

        var handler = new PublishCampaignCommandHandler(
            new FakeCampaignRepository(campaign),
            new TestCurrentUser { IsAuthenticated = false, UserId = Guid.Empty },
            new FakeDateTimeProvider(now),
            new FakeCampaignReviewStatusReader(CampaignReviewStatusContract.Approved),
            new FakeCampaignTransactionExecutor());

        var action = async () => await handler.Handle(new PublishCampaignCommand(campaign.Id), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(action);

        Assert.Equal("The current user must be authenticated to publish a campaign.", exception.Message);
    }
}

public sealed class AddContributionToCampaignCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldAddContributionAndPersistChanges()
    {
        var campaign = Campaign.Create(
            Guid.NewGuid(),
            "Launch a school robotics lab",
            "This campaign funds tools and equipment for a new school robotics lab.",
            "Education",
            new Money(5000m, "USD"),
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        campaign.Publish(new DateTime(2026, 4, 7, 12, 0, 0, DateTimeKind.Utc));

        var repository = new FakeCampaignRepository(campaign);
        var transactionExecutor = new FakeCampaignTransactionExecutor();
        var ledger = new FakeContributionLedger();
        var handler = new CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.AddContributionToCampaign.AddContributionToCampaignCommandHandler(
            repository,
            ledger,
            new FakeRewardTierRepository(),
            new FakeRewardTierReservationRepository(),
            transactionExecutor,
            new FakeCampaignRealtimeNotifier());

        var result = await handler.Handle(
            new AddContributionToCampaignCommand(campaign.Id, Guid.NewGuid(), 125m, "usd"),
            CancellationToken.None);

        Assert.Equal(campaign.Id, result.CampaignId);
        Assert.Equal(125m, result.RaisedAmount);
        Assert.Equal("USD", result.Currency);
        Assert.True(repository.WasUpdated);
        Assert.Equal(1, transactionExecutor.InvocationCount);
    }

    [Fact]
    public async Task Handle_ShouldBeIdempotent_WhenSameContributionIsAppliedTwice()
    {
        var campaign = Campaign.Create(
            Guid.NewGuid(),
            "Launch a school robotics lab",
            "This campaign funds tools and equipment for a new school robotics lab.",
            "Education",
            new Money(5000m, "USD"),
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        campaign.Publish(new DateTime(2026, 4, 7, 12, 0, 0, DateTimeKind.Utc));

        var repository = new FakeCampaignRepository(campaign);
        var transactionExecutor = new FakeCampaignTransactionExecutor();
        var ledger = new FakeContributionLedger();
        var handler = new CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.AddContributionToCampaign.AddContributionToCampaignCommandHandler(
            repository,
            ledger,
            new FakeRewardTierRepository(),
            new FakeRewardTierReservationRepository(),
            transactionExecutor,
            new FakeCampaignRealtimeNotifier());

        var contributionId = Guid.NewGuid();

        var firstResult = await handler.Handle(
            new AddContributionToCampaignCommand(campaign.Id, contributionId, 125m, "usd"),
            CancellationToken.None);

        var secondResult = await handler.Handle(
            new AddContributionToCampaignCommand(campaign.Id, contributionId, 125m, "usd"),
            CancellationToken.None);

        Assert.Equal(125m, firstResult.RaisedAmount);
        Assert.Equal(125m, secondResult.RaisedAmount);
        Assert.Equal(new Money(125m, "USD"), campaign.RaisedAmount);
        Assert.Equal(2, ledger.RecordAttemptCount);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCampaignDoesNotExist()
    {
        var transactionExecutor = new FakeCampaignTransactionExecutor();
        var handler = new CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.AddContributionToCampaign.AddContributionToCampaignCommandHandler(
            new FakeCampaignRepository(),
            new FakeContributionLedger(),
            new FakeRewardTierRepository(),
            new FakeRewardTierReservationRepository(),
            transactionExecutor,
            new FakeCampaignRealtimeNotifier());

        var action = async () => await handler.Handle(
            new AddContributionToCampaignCommand(Guid.NewGuid(), Guid.NewGuid(), 125m, "usd"),
            CancellationToken.None);

        await Assert.ThrowsAsync<KeyNotFoundException>(action);

        // The existence check now runs inside the locked critical section (the campaign is
        // loaded after the advisory lock is acquired, not before), so the executor is invoked
        // once even though the aggregate turns out not to exist.
        Assert.Equal(1, transactionExecutor.InvocationCount);
    }
}

public sealed class CancelCampaignCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldCancelCampaignAndPersistChanges()
    {
        var ownerId = Guid.NewGuid();
        var campaign = Campaign.Create(
            ownerId,
            "Restore the old theater",
            "This campaign restores the old theater as a shared cultural venue.",
            "Culture",
            new Money(12000m, "USD"),
            new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        var repository = new FakeCampaignRepository(campaign);
        var transactionExecutor = new FakeCampaignTransactionExecutor();
        var handler = new CancelCampaignCommandHandler(
            repository,
            new TestCurrentUser
            {
                UserId = ownerId,
                Permissions = [PermissionConstants.CampaignsCancel]
            },
            transactionExecutor);

        var result = await handler.Handle(new CancelCampaignCommand(campaign.Id), CancellationToken.None);

        Assert.Equal(campaign.Id, result.CampaignId);
        Assert.Equal("Cancelled", result.Status);
        Assert.Equal(CampaignStatus.Cancelled, campaign.Status);
        Assert.True(repository.WasUpdated);
        Assert.Equal(1, transactionExecutor.InvocationCount);
        Assert.Contains(campaign.DomainEvents, domainEvent => domainEvent is CampaignCancelledDomainEvent);
    }

    [Fact]
    public async Task Handle_ShouldAllowAdministratorToCancelCampaignTheyDoNotOwn()
    {
        var campaign = Campaign.Create(
            Guid.NewGuid(),
            "Restore the old theater",
            "This campaign restores the old theater as a shared cultural venue.",
            "Culture",
            new Money(12000m, "USD"),
            new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        var repository = new FakeCampaignRepository(campaign);
        var transactionExecutor = new FakeCampaignTransactionExecutor();
        var handler = new CancelCampaignCommandHandler(
            repository,
            new TestCurrentUser
            {
                UserId = Guid.NewGuid(),
                Permissions = [PermissionConstants.CampaignsManageAny]
            },
            transactionExecutor);

        var result = await handler.Handle(new CancelCampaignCommand(campaign.Id), CancellationToken.None);

        Assert.Equal("Cancelled", result.Status);
        Assert.True(repository.WasUpdated);
        Assert.Equal(1, transactionExecutor.InvocationCount);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserIsNotAuthenticated()
    {
        var campaign = Campaign.Create(
            Guid.NewGuid(),
            "Restore the old theater",
            "This campaign restores the old theater as a shared cultural venue.",
            "Culture",
            new Money(12000m, "USD"),
            new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        var handler = new CancelCampaignCommandHandler(
            new FakeCampaignRepository(campaign),
            new TestCurrentUser { IsAuthenticated = false, UserId = Guid.Empty },
            new FakeCampaignTransactionExecutor());

        var action = async () => await handler.Handle(new CancelCampaignCommand(campaign.Id), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(action);

        Assert.Equal("The current user must be authenticated to cancel a campaign.", exception.Message);
    }
}

public sealed class ListCampaignsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnCampaignsFromReadService()
    {
        IReadOnlyCollection<ListCampaignsResult> campaigns =
        [
            new(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Community Kitchen",
                "Community",
                8000m,
                "USD",
                1200m,
                "USD",
                new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                "Published",
                new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc))
        ];

        var page = new PagedResult<ListCampaignsResult>(campaigns, 2, 5, 8);
        var readService = new FakeCampaignReadService(page);
        var handler = new ListCampaignsQueryHandler(readService);
        var filter = new ListCampaignsFilter(Guid.NewGuid(), "Community", "Published");

        var result = await handler.Handle(
            new ListCampaignsQuery(new PageRequest(2, 5), filter),
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("Community Kitchen", result.Items.Single().Title);
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(5, result.PageSize);
        Assert.Equal(8, result.TotalCount);
        Assert.Equal(new PageRequest(2, 5), readService.ReceivedPageRequest);
        Assert.Equal(filter, readService.ReceivedFilter);
    }
}

public sealed class GetCampaignByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnCampaignReadModel()
    {
        var expected = new GetCampaignByIdResult(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Community Kitchen",
            "A campaign for a new community kitchen and gathering space.",
            "Community",
            8000m,
            "USD",
            1200m,
            "USD",
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            "Published",
            new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc));

        var readService = new FakeCampaignReadService(expected);
        var handler = new GetCampaignByIdQueryHandler(readService);

        var result = await handler.Handle(new GetCampaignByIdQuery(expected.Id), CancellationToken.None);

        Assert.Equal(expected, result);
        Assert.Equal(expected.Id, readService.ReceivedCampaignId);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCampaignWasNotFound()
    {
        var readService = new FakeCampaignReadService((GetCampaignByIdResult?)null);
        var handler = new GetCampaignByIdQueryHandler(readService);
        var campaignId = Guid.NewGuid();

        var action = async () => await handler.Handle(new GetCampaignByIdQuery(campaignId), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(action);

        Assert.Equal($"Campaign with id '{campaignId}' was not found.", exception.Message);
    }
}

public sealed class ContributionPaymentConfirmedApplicationEventHandlerTests
{
    [Fact]
    public async Task Handle_ShouldDispatchAddContributionCommand()
    {
        var expectedResult = new AddContributionToCampaignResult(Guid.NewGuid(), 100m, "USD");
        var commandDispatcher = new RecordingCommandDispatcher(expectedResult);
        var handler = new ContributionPaymentConfirmedApplicationEventHandler(commandDispatcher);
        var campaignId = Guid.NewGuid();

        var contributionId = Guid.NewGuid();

        await handler.Handle(
            new ContributionPaymentConfirmedApplicationEvent(contributionId, campaignId, Guid.NewGuid(), 100m, "usd"),
            CancellationToken.None);

        var command = Assert.IsType<AddContributionToCampaignCommand>(commandDispatcher.LastCommand);
        Assert.Equal(1, commandDispatcher.InvocationCount);
        Assert.Equal(campaignId, command.CampaignId);
        Assert.Equal(contributionId, command.ContributionId);
        Assert.Equal(100m, command.Amount);
        Assert.Equal("usd", command.Currency);
    }
}

public sealed class CreateCampaignCommandValidatorTests
{
    [Fact]
    public void Validate_ShouldReturnErrors_WhenCommandIsInvalid()
    {
        var validator = new CreateCampaignCommandValidator();
        var result = validator.Validate(new CreateCampaignCommand("", "", "", 0m, "US", DateTime.UtcNow.AddMinutes(-1)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateCampaignCommand.Title));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateCampaignCommand.Story));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateCampaignCommand.Category));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateCampaignCommand.GoalAmount));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateCampaignCommand.Currency));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateCampaignCommand.DeadlineUtc));
    }

    [Fact]
    public void Validate_ShouldReturnStoryError_WhenStoryIsNonEmptyButTooShort()
    {
        // Kept in sync with Campaign.ValidateStory's 20-character domain invariant — without
        // MinimumLength(20) here, this passes validation and only fails deep inside
        // Campaign.Create(), surfacing as an unstructured 400 instead of a field-level error.
        var validator = new CreateCampaignCommandValidator();
        var result = validator.Validate(new CreateCampaignCommand(
            "Valid Title",
            "Too short.",
            "Technology",
            5000m,
            "USD",
            DateTime.UtcNow.AddDays(30)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateCampaignCommand.Story));
    }
}

public sealed class PublishCampaignCommandValidatorTests
{
    [Fact]
    public void Validate_ShouldRequireCampaignId()
    {
        var validator = new PublishCampaignCommandValidator();
        var result = validator.Validate(new PublishCampaignCommand(Guid.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(PublishCampaignCommand.CampaignId));
    }
}

public sealed class CancelCampaignCommandValidatorTests
{
    [Fact]
    public void Validate_ShouldRequireCampaignId()
    {
        var validator = new CancelCampaignCommandValidator();
        var result = validator.Validate(new CancelCampaignCommand(Guid.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CancelCampaignCommand.CampaignId));
    }
}

internal sealed class FakeCampaignRepository : ICampaignRepository
{
    private readonly Campaign? _campaign;

    public FakeCampaignRepository(Campaign? campaign = null)
    {
        _campaign = campaign;
    }

    public bool WasUpdated { get; private set; }

    public Campaign? SavedCampaign { get; private set; }

    public Task AddAsync(Campaign campaign, CancellationToken cancellationToken)
    {
        SavedCampaign = campaign;
        return Task.CompletedTask;
    }

    public Task<Campaign?> GetByIdAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        if (_campaign?.Id == campaignId)
        {
            return Task.FromResult<Campaign?>(_campaign);
        }

        return Task.FromResult(SavedCampaign?.Id == campaignId ? SavedCampaign : null);
    }

    public Task UpdateAsync(Campaign campaign, CancellationToken cancellationToken)
    {
        WasUpdated = true;
        SavedCampaign = campaign;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Guid>> GetExpiredPublishedCampaignIdsAsync(DateTime asOfUtc, CancellationToken cancellationToken)
        => throw new NotSupportedException("Not used by these tests.");
}

internal sealed class FakeRewardTierRepository : IRewardTierRepository
{
    private readonly Dictionary<Guid, RewardTier> _rewardTiers = [];

    public FakeRewardTierRepository(params RewardTier[] rewardTiers)
    {
        foreach (var rewardTier in rewardTiers)
        {
            _rewardTiers[rewardTier.Id] = rewardTier;
        }
    }

    public Task AddAsync(RewardTier rewardTier, CancellationToken cancellationToken)
    {
        _rewardTiers[rewardTier.Id] = rewardTier;
        return Task.CompletedTask;
    }

    public Task<RewardTier?> GetByIdAsync(Guid rewardTierId, CancellationToken cancellationToken)
        => Task.FromResult(_rewardTiers.GetValueOrDefault(rewardTierId));

    public Task<IReadOnlyList<RewardTier>> GetByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<RewardTier>>(_rewardTiers.Values.Where(x => x.CampaignId == campaignId).ToList());

    public Task UpdateAsync(RewardTier rewardTier, CancellationToken cancellationToken)
    {
        _rewardTiers[rewardTier.Id] = rewardTier;
        return Task.CompletedTask;
    }
}

internal sealed class FakeRewardTierReservationRepository : IRewardTierReservationRepository
{
    private readonly Dictionary<Guid, RewardTierReservation> _reservations = [];

    public FakeRewardTierReservationRepository(params RewardTierReservation[] reservations)
    {
        foreach (var reservation in reservations)
        {
            _reservations[reservation.Id] = reservation;
        }
    }

    public Task AddAsync(RewardTierReservation reservation, CancellationToken cancellationToken)
    {
        _reservations[reservation.Id] = reservation;
        return Task.CompletedTask;
    }

    public Task<RewardTierReservation?> GetByIdAsync(Guid reservationId, CancellationToken cancellationToken)
        => Task.FromResult(_reservations.GetValueOrDefault(reservationId));

    public Task<IReadOnlyList<RewardTierReservation>> GetExpiredBatchAsync(DateTime nowUtc, int batchSize, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<RewardTierReservation>>(
            _reservations.Values.Where(x => x.IsExpired(nowUtc)).Take(batchSize).ToList());

    public Task UpdateAsync(RewardTierReservation reservation, CancellationToken cancellationToken)
    {
        _reservations[reservation.Id] = reservation;
        return Task.CompletedTask;
    }
}

internal sealed class FakeContributionLedger : IContributionLedger
{
    private readonly HashSet<Guid> _recordedContributionIds = [];

    public int RecordAttemptCount { get; private set; }

    public Task<bool> TryRecordAsync(
        Guid campaignId,
        Guid contributionId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken)
    {
        RecordAttemptCount++;
        return Task.FromResult(_recordedContributionIds.Add(contributionId));
    }
}

internal sealed class FakeCampaignRealtimeNotifier : ICampaignRealtimeNotifier
{
    public int NotificationCount { get; private set; }

    public Task NotifyPledgeReceivedAsync(
        Guid campaignId,
        decimal raisedAmount,
        string currency,
        CancellationToken cancellationToken = default)
    {
        NotificationCount++;
        return Task.CompletedTask;
    }

    public Task NotifyRewardTierSoldOutAsync(
        Guid campaignId,
        Guid rewardTierId,
        string title,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

internal sealed class FakeCampaignReadService : ICampaignReadService
{
    private readonly GetCampaignByIdResult? _campaign;
    private readonly PagedResult<ListCampaignsResult>? _campaignsPage;

    public FakeCampaignReadService(PagedResult<ListCampaignsResult> campaignsPage)
    {
        _campaignsPage = campaignsPage;
    }

    public FakeCampaignReadService(GetCampaignByIdResult? campaign)
    {
        _campaign = campaign;
    }

    public Guid? ReceivedCampaignId { get; private set; }
    public PageRequest? ReceivedPageRequest { get; private set; }

    public ListCampaignsFilter? ReceivedFilter { get; private set; }

    public Task<GetCampaignByIdResult?> GetByIdAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        ReceivedCampaignId = campaignId;
        return Task.FromResult(_campaign);
    }

    public Task<PagedResult<ListCampaignsResult>> ListAsync(
        PageRequest pageRequest,
        ListCampaignsFilter filter,
        CancellationToken cancellationToken)
    {
        if (_campaignsPage is null)
        {
            throw new NotSupportedException();
        }

        ReceivedPageRequest = pageRequest;
        ReceivedFilter = filter;
        return Task.FromResult(_campaignsPage);
    }
}

internal sealed class FakeCampaignReviewStatusReader : ICampaignReviewStatusReader
{
    private readonly CampaignReviewStatusContract _status;

    public FakeCampaignReviewStatusReader(CampaignReviewStatusContract status)
    {
        _status = status;
    }

    public Guid? CheckedCampaignId { get; private set; }

    public Task<GetCampaignReviewStatusByCampaignIdResult> GetCampaignReviewStatusByCampaignIdAsync(
        GetCampaignReviewStatusByCampaignIdQuery query,
        CancellationToken cancellationToken)
    {
        CheckedCampaignId = query.CampaignId;
        return Task.FromResult(new GetCampaignReviewStatusByCampaignIdResult(query.CampaignId, _status));
    }
}

internal sealed class FakeDateTimeProvider : IDateTimeProvider
{
    public FakeDateTimeProvider(DateTime utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTime UtcNow { get; }
}

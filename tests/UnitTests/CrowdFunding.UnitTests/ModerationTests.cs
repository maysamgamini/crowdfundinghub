using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCreated;
using CrowdFunding.Modules.Moderation.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Moderation.Application.Abstractions.Services;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.ApproveCampaignReview;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.CreateCampaignReview;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.RejectCampaignReview;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Events;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Queries.GetCampaignReviewByCampaignId;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Queries.GetCampaignReviewStatusByCampaignId;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using CrowdFunding.Modules.Moderation.Contracts.Queries.GetCampaignReviewStatusByCampaignId;
using CrowdFunding.Modules.Moderation.Domain.Aggregates;
using CrowdFunding.Modules.Moderation.Domain.Enums;
using CrowdFunding.Modules.Moderation.Domain.Events;

namespace CrowdFunding.UnitTests;

public sealed class CampaignReviewDomainTests
{
    [Fact]
    public void Create_ShouldInitializePendingReview()
    {
        var createdAtUtc = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);

        var review = CampaignReview.Create(Guid.NewGuid(), createdAtUtc);

        Assert.Equal(CampaignReviewStatus.Pending, review.Status);
        Assert.Equal(createdAtUtc, review.CreatedAtUtc);
        Assert.Empty(review.DomainEvents);
    }

    [Fact]
    public void Approve_ShouldMoveReviewToApproved_AndRaiseDomainEvent()
    {
        var review = CampaignReview.Create(Guid.NewGuid(), new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        review.Approve(Guid.NewGuid(), "Approved after verification.", new DateTime(2026, 4, 7, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(CampaignReviewStatus.Approved, review.Status);
        Assert.NotNull(review.ModeratorId);
        Assert.NotNull(review.ReviewedAtUtc);
        Assert.Contains(review.DomainEvents, domainEvent => domainEvent is CampaignReviewApprovedDomainEvent);
    }

    [Fact]
    public void Reject_ShouldMoveReviewToRejected_AndRaiseDomainEvent()
    {
        var review = CampaignReview.Create(Guid.NewGuid(), new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        review.Reject(Guid.NewGuid(), "Missing required details.", new DateTime(2026, 4, 7, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(CampaignReviewStatus.Rejected, review.Status);
        Assert.NotNull(review.ModeratorId);
        Assert.NotNull(review.ReviewedAtUtc);
        Assert.Contains(review.DomainEvents, domainEvent => domainEvent is CampaignReviewRejectedDomainEvent);
    }

    [Fact]
    public void Approve_ShouldTrimNotes()
    {
        var review = CampaignReview.Create(Guid.NewGuid(), new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        review.Approve(Guid.NewGuid(), "  Approved after verification.  ", new DateTime(2026, 4, 7, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal("Approved after verification.", review.Notes);
    }

    [Fact]
    public void Approve_ShouldThrow_WhenReviewIsNotPending()
    {
        var review = CampaignReview.Create(Guid.NewGuid(), new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));
        review.Approve(Guid.NewGuid(), "Approved after verification.", new DateTime(2026, 4, 7, 12, 0, 0, DateTimeKind.Utc));

        var action = () => review.Approve(Guid.NewGuid(), "Approve again.", new DateTime(2026, 4, 8, 12, 0, 0, DateTimeKind.Utc));

        var exception = Assert.Throws<InvalidOperationException>(action);

        Assert.Equal("Only pending campaign reviews can be updated.", exception.Message);
    }
}

public sealed class CreateCampaignReviewCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldCreatePendingReview()
    {
        var repository = new FakeCampaignReviewRepository();
        var transactionExecutor = new FakeModerationTransactionExecutor();
        var handler = new CreateCampaignReviewCommandHandler(
            repository,
            new FakeModerationDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)),
            transactionExecutor);

        var result = await handler.Handle(new CreateCampaignReviewCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.NotNull(repository.SavedReview);
        Assert.Equal(result.CampaignReviewId, repository.SavedReview!.Id);
        Assert.Equal("Pending", result.Status);
        Assert.Equal(1, transactionExecutor.InvocationCount);
    }

    [Fact]
    public async Task Handle_ShouldReturnExistingReviewWithoutCreatingAnotherOne()
    {
        var existingReview = CampaignReview.Create(Guid.NewGuid(), new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));
        var repository = new FakeCampaignReviewRepository(existingReview);
        var transactionExecutor = new FakeModerationTransactionExecutor();
        var handler = new CreateCampaignReviewCommandHandler(
            repository,
            new FakeModerationDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)),
            transactionExecutor);

        var result = await handler.Handle(new CreateCampaignReviewCommand(existingReview.CampaignId), CancellationToken.None);

        Assert.Equal(existingReview.Id, result.CampaignReviewId);
        Assert.Equal("Pending", result.Status);
        Assert.Equal(0, transactionExecutor.InvocationCount);
    }
}

public sealed class ApproveCampaignReviewCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldApprovePendingReview()
    {
        var review = CampaignReview.Create(Guid.NewGuid(), new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));
        var repository = new FakeCampaignReviewRepository(review);
        var transactionExecutor = new FakeModerationTransactionExecutor();
        var handler = new ApproveCampaignReviewCommandHandler(
            repository,
            new TestCurrentUser
            {
                UserId = Guid.NewGuid(),
                Permissions = [PermissionConstants.ModerationReview]
            },
            new FakeModerationDateTimeProvider(new DateTime(2026, 4, 7, 12, 0, 0, DateTimeKind.Utc)),
            transactionExecutor);

        var result = await handler.Handle(
            new ApproveCampaignReviewCommand(review.CampaignId, "Looks good."),
            CancellationToken.None);

        Assert.Equal("Approved", result.Status);
        Assert.Equal(CampaignReviewStatus.Approved, review.Status);
        Assert.True(repository.WasUpdated);
        Assert.Equal(1, transactionExecutor.InvocationCount);
        Assert.Contains(review.DomainEvents, domainEvent => domainEvent is CampaignReviewApprovedDomainEvent);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserIsNotAuthenticated()
    {
        var review = CampaignReview.Create(Guid.NewGuid(), new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));
        var handler = new ApproveCampaignReviewCommandHandler(
            new FakeCampaignReviewRepository(review),
            new TestCurrentUser { IsAuthenticated = false, UserId = Guid.Empty },
            new FakeModerationDateTimeProvider(new DateTime(2026, 4, 7, 12, 0, 0, DateTimeKind.Utc)),
            new FakeModerationTransactionExecutor());

        var action = async () => await handler.Handle(
            new ApproveCampaignReviewCommand(review.CampaignId, "Looks good."),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(action);

        Assert.Equal("The current user must be authenticated to approve a campaign review.", exception.Message);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserLacksPermission()
    {
        var review = CampaignReview.Create(Guid.NewGuid(), new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));
        var handler = new ApproveCampaignReviewCommandHandler(
            new FakeCampaignReviewRepository(review),
            new TestCurrentUser(),
            new FakeModerationDateTimeProvider(new DateTime(2026, 4, 7, 12, 0, 0, DateTimeKind.Utc)),
            new FakeModerationTransactionExecutor());

        var action = async () => await handler.Handle(
            new ApproveCampaignReviewCommand(review.CampaignId, "Looks good."),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ForbiddenAccessException>(action);

        Assert.Equal("The current user does not have permission to review campaigns.", exception.Message);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenReviewDoesNotExist()
    {
        var transactionExecutor = new FakeModerationTransactionExecutor();
        var campaignId = Guid.NewGuid();
        var handler = new ApproveCampaignReviewCommandHandler(
            new FakeCampaignReviewRepository(),
            new TestCurrentUser
            {
                UserId = Guid.NewGuid(),
                Permissions = [PermissionConstants.ModerationReview]
            },
            new FakeModerationDateTimeProvider(new DateTime(2026, 4, 7, 12, 0, 0, DateTimeKind.Utc)),
            transactionExecutor);

        var action = async () => await handler.Handle(
            new ApproveCampaignReviewCommand(campaignId, "Looks good."),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(action);

        Assert.Equal($"Campaign review for campaign '{campaignId}' was not found.", exception.Message);
        Assert.Equal(0, transactionExecutor.InvocationCount);
    }
}

public sealed class RejectCampaignReviewCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldRejectPendingReview()
    {
        var review = CampaignReview.Create(Guid.NewGuid(), new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));
        var repository = new FakeCampaignReviewRepository(review);
        var transactionExecutor = new FakeModerationTransactionExecutor();
        var handler = new RejectCampaignReviewCommandHandler(
            repository,
            new TestCurrentUser
            {
                UserId = Guid.NewGuid(),
                Permissions = [PermissionConstants.ModerationReview]
            },
            new FakeModerationDateTimeProvider(new DateTime(2026, 4, 7, 12, 0, 0, DateTimeKind.Utc)),
            transactionExecutor);

        var result = await handler.Handle(
            new RejectCampaignReviewCommand(review.CampaignId, "Needs more evidence."),
            CancellationToken.None);

        Assert.Equal("Rejected", result.Status);
        Assert.Equal(CampaignReviewStatus.Rejected, review.Status);
        Assert.True(repository.WasUpdated);
        Assert.Equal(1, transactionExecutor.InvocationCount);
        Assert.Contains(review.DomainEvents, domainEvent => domainEvent is CampaignReviewRejectedDomainEvent);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserIsNotAuthenticated()
    {
        var review = CampaignReview.Create(Guid.NewGuid(), new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));
        var handler = new RejectCampaignReviewCommandHandler(
            new FakeCampaignReviewRepository(review),
            new TestCurrentUser { IsAuthenticated = false, UserId = Guid.Empty },
            new FakeModerationDateTimeProvider(new DateTime(2026, 4, 7, 12, 0, 0, DateTimeKind.Utc)),
            new FakeModerationTransactionExecutor());

        var action = async () => await handler.Handle(
            new RejectCampaignReviewCommand(review.CampaignId, "Needs more evidence."),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(action);

        Assert.Equal("The current user must be authenticated to reject a campaign review.", exception.Message);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserLacksPermission()
    {
        var review = CampaignReview.Create(Guid.NewGuid(), new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));
        var handler = new RejectCampaignReviewCommandHandler(
            new FakeCampaignReviewRepository(review),
            new TestCurrentUser(),
            new FakeModerationDateTimeProvider(new DateTime(2026, 4, 7, 12, 0, 0, DateTimeKind.Utc)),
            new FakeModerationTransactionExecutor());

        var action = async () => await handler.Handle(
            new RejectCampaignReviewCommand(review.CampaignId, "Needs more evidence."),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ForbiddenAccessException>(action);

        Assert.Equal("The current user does not have permission to review campaigns.", exception.Message);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenReviewDoesNotExist()
    {
        var transactionExecutor = new FakeModerationTransactionExecutor();
        var campaignId = Guid.NewGuid();
        var handler = new RejectCampaignReviewCommandHandler(
            new FakeCampaignReviewRepository(),
            new TestCurrentUser
            {
                UserId = Guid.NewGuid(),
                Permissions = [PermissionConstants.ModerationReview]
            },
            new FakeModerationDateTimeProvider(new DateTime(2026, 4, 7, 12, 0, 0, DateTimeKind.Utc)),
            transactionExecutor);

        var action = async () => await handler.Handle(
            new RejectCampaignReviewCommand(campaignId, "Needs more evidence."),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(action);

        Assert.Equal($"Campaign review for campaign '{campaignId}' was not found.", exception.Message);
        Assert.Equal(0, transactionExecutor.InvocationCount);
    }
}

public sealed class GetCampaignReviewByCampaignIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnReviewFromReadService()
    {
        var expected = new GetCampaignReviewByCampaignIdResult(
            Guid.NewGuid(),
            "Pending",
            null,
            null,
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc),
            null);

        var handler = new GetCampaignReviewByCampaignIdQueryHandler(new FakeCampaignReviewReadService(expected));

        var result = await handler.Handle(new GetCampaignReviewByCampaignIdQuery(expected.CampaignId), CancellationToken.None);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenReviewWasNotFound()
    {
        var campaignId = Guid.NewGuid();
        var handler = new GetCampaignReviewByCampaignIdQueryHandler(new FakeCampaignReviewReadService(null));

        var action = async () => await handler.Handle(new GetCampaignReviewByCampaignIdQuery(campaignId), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(action);

        Assert.Equal($"Campaign review for campaign '{campaignId}' was not found.", exception.Message);
    }
}

public sealed class GetCampaignReviewStatusByCampaignIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnStatusFromReadService()
    {
        var review = new GetCampaignReviewByCampaignIdResult(
            Guid.NewGuid(),
            "Approved",
            Guid.NewGuid(),
            "Looks good.",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 7, 12, 0, 0, DateTimeKind.Utc));
        var readService = new FakeCampaignReviewReadService(review);
        var handler = new GetCampaignReviewStatusByCampaignIdQueryHandler(readService);

        var result = await handler.Handle(new GetCampaignReviewStatusByCampaignIdQuery(review.CampaignId), CancellationToken.None);

        Assert.Equal(new GetCampaignReviewStatusByCampaignIdResult(review.CampaignId, "Approved"), result);
        Assert.Equal(review.CampaignId, readService.ReceivedCampaignId);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenReviewWasNotFound()
    {
        var campaignId = Guid.NewGuid();
        var handler = new GetCampaignReviewStatusByCampaignIdQueryHandler(new FakeCampaignReviewReadService(null));

        var action = async () => await handler.Handle(new GetCampaignReviewStatusByCampaignIdQuery(campaignId), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(action);

        Assert.Equal($"Campaign review for campaign '{campaignId}' was not found.", exception.Message);
    }
}

public sealed class CampaignCreatedApplicationEventHandlerTests
{
    [Fact]
    public async Task Handle_ShouldDispatchCreateCampaignReviewCommand()
    {
        var campaignReviewId = Guid.NewGuid();
        var commandDispatcher = new RecordingCommandDispatcher(new CreateCampaignReviewResult(campaignReviewId, "Pending"));
        var handler = new CampaignCreatedApplicationEventHandler(commandDispatcher);
        var campaignId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        await handler.Handle(new CampaignCreatedApplicationEvent(campaignId, ownerId), CancellationToken.None);

        var command = Assert.IsType<CreateCampaignReviewCommand>(commandDispatcher.LastCommand);
        Assert.Equal(1, commandDispatcher.InvocationCount);
        Assert.Equal(campaignId, command.CampaignId);
    }
}

public sealed class ApproveCampaignReviewCommandValidatorTests
{
    [Fact]
    public void Validate_ShouldRequireCampaignId()
    {
        var validator = new ApproveCampaignReviewCommandValidator();
        var result = validator.Validate(new ApproveCampaignReviewCommand(Guid.Empty, "Looks good."));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ApproveCampaignReviewCommand.CampaignId));
    }
}

public sealed class RejectCampaignReviewCommandValidatorTests
{
    [Fact]
    public void Validate_ShouldRequireCampaignId()
    {
        var validator = new RejectCampaignReviewCommandValidator();
        var result = validator.Validate(new RejectCampaignReviewCommand(Guid.Empty, "Needs more evidence."));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RejectCampaignReviewCommand.CampaignId));
    }
}

internal sealed class FakeCampaignReviewRepository : ICampaignReviewRepository
{
    private readonly CampaignReview? _review;

    public FakeCampaignReviewRepository(CampaignReview? review = null)
    {
        _review = review;
    }

    public CampaignReview? SavedReview { get; private set; }

    public bool WasUpdated { get; private set; }

    public Task AddAsync(CampaignReview campaignReview, CancellationToken cancellationToken)
    {
        SavedReview = campaignReview;
        return Task.CompletedTask;
    }

    public Task<CampaignReview?> GetByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        if (_review?.CampaignId == campaignId)
        {
            return Task.FromResult<CampaignReview?>(_review);
        }

        return Task.FromResult(SavedReview?.CampaignId == campaignId ? SavedReview : null);
    }

    public Task UpdateAsync(CampaignReview campaignReview, CancellationToken cancellationToken)
    {
        WasUpdated = true;
        SavedReview = campaignReview;
        return Task.CompletedTask;
    }
}

internal sealed class FakeCampaignReviewReadService : ICampaignReviewReadService
{
    private readonly GetCampaignReviewByCampaignIdResult? _result;

    public FakeCampaignReviewReadService(GetCampaignReviewByCampaignIdResult? result)
    {
        _result = result;
    }

    public Guid? ReceivedCampaignId { get; private set; }

    public Task<GetCampaignReviewByCampaignIdResult?> GetByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        ReceivedCampaignId = campaignId;
        return Task.FromResult(_result);
    }
}

internal sealed class FakeModerationDateTimeProvider : IModerationDateTimeProvider
{
    public FakeModerationDateTimeProvider(DateTime utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTime UtcNow { get; }
}

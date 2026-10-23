using CrowdFunding.BuildingBlocks.Application.Pagination;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Queries.GetContributionById;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Queries.ListContributionsByCampaign;
using CrowdFunding.Modules.Identity.Contracts.Authorization;

namespace CrowdFunding.UnitTests;

/// <summary>
/// Who may read pledges. A pledge carries the contributor id and payment references, so reading
/// one is not something an anonymous caller, or any other backer, should be able to do.
/// </summary>
public sealed class ContributionReadAuthorizationTests
{
    private readonly Guid _campaignId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _backerId = Guid.NewGuid();
    private readonly Guid _otherBackerId = Guid.NewGuid();

    private ListContributionsByCampaignResult Pledge(Guid contributorId) => new(
        Guid.NewGuid(), _campaignId, contributorId, 75m, "USD", "Failed", "PAY-123", "card declined",
        new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc), null);

    private (FakeContributionReadService Service, ListContributionsByCampaignResult Mine, ListContributionsByCampaignResult Theirs) Data()
    {
        var mine = Pledge(_backerId);
        var theirs = Pledge(_otherBackerId);
        var page = new PagedResult<ListContributionsByCampaignResult>([mine, theirs], 1, 10, 2);
        return (new FakeContributionReadService(page), mine, theirs);
    }

    private TestCurrentUser Backer() => new() { UserId = _backerId };

    private TestCurrentUser Owner() => new() { UserId = _ownerId };

    private static TestCurrentUser Manager() => new() { UserId = Guid.NewGuid(), Permissions = [PermissionConstants.ContributionsPaymentsManage] };

    private ListContributionsByCampaignQueryHandler ListHandler(FakeContributionReadService service, TestCurrentUser user)
        => new(service, user, new FakeActiveCampaignCacheRepository(exists: true, isActive: true, ownerId: _ownerId));

    private GetContributionByIdQueryHandler GetHandler(FakeContributionReadService service, TestCurrentUser user)
        => new(service, user, new FakeActiveCampaignCacheRepository(exists: true, isActive: true, ownerId: _ownerId));

    private ListContributionsByCampaignQuery ListQuery(Guid? contributorId = null)
        => new(_campaignId, new PageRequest(1, 10), new ListContributionsByCampaignFilter(contributorId, null, null));

    [Fact]
    public async Task List_AnAnonymousCaller_IsRefused()
    {
        var (service, _, _) = Data();
        var anonymous = new TestCurrentUser { UserId = Guid.Empty };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            ListHandler(service, anonymous).Handle(ListQuery(), CancellationToken.None));
        Assert.Null(service.ReceivedFilter);
    }

    [Fact]
    public async Task List_ABacker_IsRestrictedToTheirOwnPledges()
    {
        var (service, _, _) = Data();

        await ListHandler(service, Backer()).Handle(ListQuery(), CancellationToken.None);

        Assert.Equal(_backerId, service.ReceivedFilter!.ContributorId);
    }

    [Fact]
    public async Task List_ABackerAskingForSomeoneElsesPledges_GetsAnEmptyPageWithoutTouchingTheData()
    {
        var (service, _, _) = Data();

        var result = await ListHandler(service, Backer()).Handle(ListQuery(_otherBackerId), CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Null(service.ReceivedFilter);
    }

    [Fact]
    public async Task List_TheCampaignOwner_SeesEveryPledgeButNotPaymentReferences()
    {
        var (service, _, _) = Data();

        var result = await ListHandler(service, Owner()).Handle(ListQuery(), CancellationToken.None);

        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, item =>
        {
            Assert.Null(item.PaymentReference);
            Assert.Null(item.FailureReason);
        });
    }

    [Fact]
    public async Task List_APaymentsManager_SeesEverythingUnredacted()
    {
        var (service, _, _) = Data();

        var result = await ListHandler(service, Manager()).Handle(ListQuery(), CancellationToken.None);

        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, item => Assert.Equal("PAY-123", item.PaymentReference));
    }

    [Fact]
    public async Task Get_AnAnonymousCaller_IsRefused()
    {
        var (service, mine, _) = Data();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            GetHandler(service, new TestCurrentUser { UserId = Guid.Empty })
                .Handle(new GetContributionByIdQuery(_campaignId, mine.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Get_TheBackerWhoMadeThePledge_SeesItInFull()
    {
        var (service, mine, _) = Data();

        var result = await GetHandler(service, Backer()).Handle(new GetContributionByIdQuery(_campaignId, mine.Id), CancellationToken.None);

        Assert.Equal("PAY-123", result.PaymentReference);
        Assert.Equal("card declined", result.FailureReason);
    }

    [Fact]
    public async Task Get_AnotherBacker_GetsNotFound_NotForbidden()
    {
        var (service, _, theirs) = Data();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            GetHandler(service, Backer()).Handle(new GetContributionByIdQuery(_campaignId, theirs.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Get_TheCampaignOwner_SeesAPledgeWithoutItsPaymentReferences()
    {
        var (service, mine, _) = Data();

        var result = await GetHandler(service, Owner()).Handle(new GetContributionByIdQuery(_campaignId, mine.Id), CancellationToken.None);

        Assert.Equal(mine.Id, result.Id);
        Assert.Null(result.PaymentReference);
        Assert.Null(result.ExternalPaymentIntentId);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public async Task Get_APaymentsManager_SeesTheFullPledge()
    {
        var (service, _, theirs) = Data();

        var result = await GetHandler(service, Manager()).Handle(new GetContributionByIdQuery(_campaignId, theirs.Id), CancellationToken.None);

        Assert.Equal("PAY-123", result.PaymentReference);
    }
}

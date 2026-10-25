using CrowdFunding.BuildingBlocks.Application.Pagination;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Queries.GetPledgeById;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Queries.ListPledgesByCampaign;
using CrowdFunding.Modules.Identity.Contracts.Authorization;

namespace CrowdFunding.UnitTests;

/// <summary>
/// Who may read pledges. A pledge carries the backer id and payment references, so reading
/// one is not something an anonymous caller, or any other backer, should be able to do.
/// </summary>
public sealed class PledgeReadAuthorizationTests
{
    private readonly Guid _campaignId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _backerId = Guid.NewGuid();
    private readonly Guid _otherBackerId = Guid.NewGuid();

    private ListPledgesByCampaignResult Pledge(Guid backerId) => new(
        Guid.NewGuid(), _campaignId, backerId, 75m, "USD", "Failed", "PAY-123", "card declined",
        new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc), null);

    private (FakePledgeReadService Service, ListPledgesByCampaignResult Mine, ListPledgesByCampaignResult Theirs) Data()
    {
        var mine = Pledge(_backerId);
        var theirs = Pledge(_otherBackerId);
        var page = new PagedResult<ListPledgesByCampaignResult>([mine, theirs], 1, 10, 2);
        return (new FakePledgeReadService(page), mine, theirs);
    }

    private TestCurrentUser Backer() => new() { UserId = _backerId };

    private TestCurrentUser Owner() => new() { UserId = _ownerId };

    private static TestCurrentUser Manager() => new() { UserId = Guid.NewGuid(), Permissions = [PermissionConstants.PledgesPaymentsManage] };

    private ListPledgesByCampaignQueryHandler ListHandler(FakePledgeReadService service, TestCurrentUser user)
        => new(service, user, new FakeActiveCampaignCacheRepository(exists: true, isActive: true, ownerId: _ownerId));

    private GetPledgeByIdQueryHandler GetHandler(FakePledgeReadService service, TestCurrentUser user)
        => new(service, user, new FakeActiveCampaignCacheRepository(exists: true, isActive: true, ownerId: _ownerId));

    private ListPledgesByCampaignQuery ListQuery(Guid? backerId = null)
        => new(_campaignId, new PageRequest(1, 10), new ListPledgesByCampaignFilter(backerId, null, null));

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

        Assert.Equal(_backerId, service.ReceivedFilter!.BackerId);
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
                .Handle(new GetPledgeByIdQuery(_campaignId, mine.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Get_TheBackerWhoMadeThePledge_SeesItInFull()
    {
        var (service, mine, _) = Data();

        var result = await GetHandler(service, Backer()).Handle(new GetPledgeByIdQuery(_campaignId, mine.Id), CancellationToken.None);

        Assert.Equal("PAY-123", result.PaymentReference);
        Assert.Equal("card declined", result.FailureReason);
    }

    [Fact]
    public async Task Get_AnotherBacker_GetsNotFound_NotForbidden()
    {
        var (service, _, theirs) = Data();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            GetHandler(service, Backer()).Handle(new GetPledgeByIdQuery(_campaignId, theirs.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Get_TheCampaignOwner_SeesAPledgeWithoutItsPaymentReferences()
    {
        var (service, mine, _) = Data();

        var result = await GetHandler(service, Owner()).Handle(new GetPledgeByIdQuery(_campaignId, mine.Id), CancellationToken.None);

        Assert.Equal(mine.Id, result.Id);
        Assert.Null(result.PaymentReference);
        Assert.Null(result.ExternalPaymentIntentId);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public async Task Get_APaymentsManager_SeesTheFullPledge()
    {
        var (service, _, theirs) = Data();

        var result = await GetHandler(service, Manager()).Handle(new GetPledgeByIdQuery(_campaignId, theirs.Id), CancellationToken.None);

        Assert.Equal("PAY-123", result.PaymentReference);
    }
}

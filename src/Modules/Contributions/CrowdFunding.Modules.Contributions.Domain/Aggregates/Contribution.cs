using CrowdFunding.BuildingBlocks.Domain.ValueObjects;

namespace CrowdFunding.Modules.Contributions.Domain.Aggregates;

public sealed class Contribution
{
    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public Guid ContributorId { get; private set; }
    public Money Money { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }

    private Contribution()
    {
    }

    private Contribution(
        Guid id,
        Guid campaignId,
        Guid contributorId,
        Money money,
        DateTime createdAtUtc)
    {
        Id = id;
        CampaignId = campaignId;
        ContributorId = contributorId;
        Money = money;
        CreatedAtUtc = createdAtUtc;
    }

    public static Contribution Create(
        Guid campaignId,
        Guid contributorId,
        decimal amount,
        string currency,
        DateTime createdAtUtc)
    {
        if (campaignId == Guid.Empty)
        {
            throw new ArgumentException("CampaignId is required.", nameof(campaignId));
        }

        if (contributorId == Guid.Empty)
        {
            throw new ArgumentException("ContributorId is required.", nameof(contributorId));
        }

        var money = new Money(amount, currency);

        if (money.Amount <= 0)
        {
            throw new ArgumentException("Contribution amount must be greater than zero.", nameof(amount));
        }

        return new Contribution(
            Guid.NewGuid(),
            campaignId,
            contributorId,
            money,
            createdAtUtc);
    }
}

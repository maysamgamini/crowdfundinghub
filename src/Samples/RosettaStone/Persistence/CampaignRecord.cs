namespace CrowdFunding.Samples.RosettaStone.Persistence;

/// <summary>
/// The flat, non-aggregate record written by both Tier 1 (Minimal API) and Tier 2 (Pragmatic
/// CQRS) slices. Deliberately not an EF Core owned-type/value-object model and not the real
/// <c>Campaign</c> domain aggregate — it lives in its own <c>rosetta</c> schema, isolated from
/// every production module, so this sample can be deleted or extracted without touching
/// anything else in the monolith.
/// </summary>
public sealed class CampaignRecord
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Story { get; set; } = string.Empty;

    public decimal TargetAmount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}

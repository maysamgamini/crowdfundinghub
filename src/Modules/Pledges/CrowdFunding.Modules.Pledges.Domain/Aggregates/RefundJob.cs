namespace CrowdFunding.Modules.Pledges.Domain.Aggregates;

/// <summary>
/// The refund of every paid pledge of a campaign that failed or was cancelled. It is a long job in small steps: the
/// progress lives in the pledges themselves (a pledge is either still <c>Succeeded</c> or already <c>Refunded</c>), so a
/// stopped worker, a crash or a slow provider leaves nothing to repair and the next pass carries on where the last
/// stopped. There is one job per campaign, and the database enforces it with a unique index on the campaign.
/// </summary>
public sealed class RefundJob
{
    public Guid Id { get; private set; }

    public Guid CampaignId { get; private set; }

    public RefundJobStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>When the job ended, whichever way it ended.</summary>
    public DateTime? FinishedAtUtc { get; private set; }

    private RefundJob()
    {
    }

    public static RefundJob Create(Guid campaignId, DateTime nowUtc)
        => new() { Id = Guid.NewGuid(), CampaignId = campaignId, Status = RefundJobStatus.Open, CreatedAtUtc = nowUtc };

    public void Complete(DateTime nowUtc) => Finish(RefundJobStatus.Completed, nowUtc);

    public void NeedAttention(DateTime nowUtc) => Finish(RefundJobStatus.NeedsAttention, nowUtc);

    private void Finish(RefundJobStatus status, DateTime nowUtc)
    {
        if (Status != RefundJobStatus.Open)
        {
            throw new InvalidOperationException($"Only an open refund job can finish. Current status: '{Status}'.");
        }

        Status = status;
        FinishedAtUtc = nowUtc;
    }
}

public enum RefundJobStatus
{
    /// <summary>Some pledges are still to be refunded.</summary>
    Open = 1,

    /// <summary>Every paid pledge of the campaign was refunded.</summary>
    Completed = 2,

    /// <summary>Some pledges used up their refund attempts, so a person has to look at them.</summary>
    NeedsAttention = 3,
}

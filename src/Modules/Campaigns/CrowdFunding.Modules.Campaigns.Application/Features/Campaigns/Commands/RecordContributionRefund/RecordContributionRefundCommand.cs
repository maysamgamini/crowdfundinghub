namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.RecordContributionRefund;

/// <summary>
/// Appends the compensating ledger entry for a refunded contribution, reducing the campaign's
/// raised amount by exactly what that contribution had added.
/// </summary>
/// <param name="CampaignId">The campaign the contribution was made to.</param>
/// <param name="ContributionId">The refunded contribution.</param>
/// <param name="Amount">The refunded amount (positive).</param>
/// <param name="Currency">The ISO 4217 currency.</param>
public sealed record RecordContributionRefundCommand(Guid CampaignId, Guid ContributionId, decimal Amount, string Currency);

/// <summary>
/// The outcome of <see cref="RecordContributionRefundCommand"/>.
/// </summary>
/// <param name="CampaignId">The campaign.</param>
/// <param name="RaisedAmount">The campaign's raised amount afterwards (the sum of its ledger).</param>
/// <param name="Reversed">
/// <see langword="true"/> when this call appended the refund entry; <see langword="false"/> when the
/// refund was already recorded (redelivery) or the contribution had never been credited.
/// </param>
public sealed record RecordContributionRefundResult(Guid CampaignId, decimal RaisedAmount, bool Reversed);

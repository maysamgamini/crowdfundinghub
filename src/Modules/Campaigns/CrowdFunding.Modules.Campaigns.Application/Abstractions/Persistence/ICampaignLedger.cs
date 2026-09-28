using CrowdFunding.Modules.Campaigns.Domain.Aggregates;

namespace CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;

/// <summary>
/// The campaign's append-only funding ledger and its raised-amount snapshot.
/// </summary>
/// <remarks>
/// Must be called inside the campaign's transaction while holding its advisory lock (every
/// campaign writer takes it through <c>ICampaignTransactionExecutor</c>): the lock serializes
/// appends with each other and with cancel, publish and completion, so the snapshot recomputed
/// by <see cref="AppendAsync"/> always sees every committed entry.
/// </remarks>
public interface ICampaignLedger
{
    /// <summary>
    /// Appends <paramref name="entry"/> unless an entry of the same kind already exists for its
    /// contribution (and, for a refund, unless there is no credited pledge to reverse), then sets
    /// the campaign's raised amount to the sum of all its entries.
    /// </summary>
    /// <param name="entry">The pledge or refund entry produced by the <see cref="Campaign"/> aggregate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Whether the entry was appended, and the ledger total afterwards.</returns>
    Task<CampaignLedgerAppendResult> AppendAsync(CampaignLedgerEntry entry, CancellationToken cancellationToken);
}

/// <summary>
/// The outcome of <see cref="ICampaignLedger.AppendAsync"/>.
/// </summary>
/// <param name="Appended"><see langword="false"/> when the database refused the entry as a duplicate (or a refund with nothing to reverse).</param>
/// <param name="LedgerTotal">The campaign's raised amount: the sum of its ledger entries after the call.</param>
public sealed record CampaignLedgerAppendResult(bool Appended, decimal LedgerTotal);

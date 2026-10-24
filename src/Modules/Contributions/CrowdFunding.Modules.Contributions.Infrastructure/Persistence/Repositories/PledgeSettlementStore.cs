using CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Contributions.Domain.Aggregates;
using CrowdFunding.Modules.Contributions.Domain.Enums;
using CrowdFunding.Modules.Contributions.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CrowdFunding.Modules.Contributions.Infrastructure.Persistence.Repositories;

public sealed class PledgeSettlementStore : IPledgeSettlementStore
{
    private readonly ContributionsDbContext _dbContext;

    public PledgeSettlementStore(ContributionsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Contribution>> GetBatchAsync(
        Guid campaignId,
        IReadOnlyCollection<ContributionStatus> statuses,
        int batchSize,
        CancellationToken cancellationToken)
        => await _dbContext.Contributions
            .Where(x => x.CampaignId == campaignId && statuses.Contains(x.Status))
            .OrderBy(x => x.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Contribution>> ClaimDueForChargeAsync(
        DateTime nowUtc,
        TimeSpan lease,
        int limit,
        CancellationToken cancellationToken)
    {
        var entity = _dbContext.Model.FindEntityType(typeof(Contribution))!;
        var table = $"\"{entity.GetSchema()}\".\"{entity.GetTableName()}\"";
        var leaseEnds = nowUtc + lease;

        // One statement claims and marks the rows, so no other worker can slip in between. A pledge waiting for
        // its charge moves to Charging and its attempt number goes up. A pledge already Charging whose lease ran
        // out keeps its attempt number: the attempt may well have reached the provider, and resuming it with the
        // same idempotency key is what makes that safe.
        // The statement is run directly and only the claimed ids are read back: EF Core wraps raw SQL that returns
        // entities in a SELECT subquery, and PostgreSQL does not allow an UPDATE ... RETURNING inside one.
        var sql = $$"""
            WITH due AS (
                SELECT "Id" FROM {{table}}
                WHERE ("Status" = 'ToCharge' AND "NextChargeAtUtc" <= @now)
                   OR ("Status" = 'Charging' AND "ChargeLeaseUntilUtc" < @now)
                ORDER BY COALESCE("NextChargeAtUtc", "ChargeLeaseUntilUtc"), "Id"
                FOR UPDATE SKIP LOCKED
                LIMIT @limit
            )
            UPDATE {{table}} c
            SET "Status" = 'Charging',
                "ChargeLeaseUntilUtc" = @leaseEnds,
                "ChargeAttempts" = CASE WHEN c."Status" = 'ToCharge' THEN c."ChargeAttempts" + 1 ELSE c."ChargeAttempts" END
            FROM due
            WHERE c."Id" = due."Id"
            RETURNING c."Id";
            """;

        var ids = new List<Guid>();
        var connection = _dbContext.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;
        if (wasClosed)
        {
            await _dbContext.Database.OpenConnectionAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = _dbContext.Database.CurrentTransaction?.GetDbTransaction();
            command.Parameters.Add(new Npgsql.NpgsqlParameter("now", NpgsqlTypes.NpgsqlDbType.TimestampTz) { Value = nowUtc });
            command.Parameters.Add(new Npgsql.NpgsqlParameter("limit", limit));
            command.Parameters.Add(new Npgsql.NpgsqlParameter("leaseEnds", NpgsqlTypes.NpgsqlDbType.TimestampTz) { Value = leaseEnds });

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add(reader.GetGuid(0));
            }
        }
        finally
        {
            if (wasClosed)
            {
                await _dbContext.Database.CloseConnectionAsync();
            }
        }

        if (ids.Count == 0)
        {
            return [];
        }

        return await _dbContext.Contributions
            .AsTracking()
            .Where(c => ids.Contains(c.Id))
            .OrderBy(c => c.NextChargeAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<ChargeAttempt> GetOrCreateAttemptAsync(
        Guid contributionId,
        int attemptNumber,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.ChargeAttempts
            .SingleOrDefaultAsync(x => x.ContributionId == contributionId && x.AttemptNumber == attemptNumber, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var attempt = new ChargeAttempt(contributionId, attemptNumber, ChargeAttempt.KeyFor(contributionId, attemptNumber), nowUtc);
        _dbContext.ChargeAttempts.Add(attempt);
        return attempt;
    }
}

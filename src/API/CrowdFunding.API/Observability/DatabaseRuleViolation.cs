using System.Net;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CrowdFunding.API.Observability;

/// <summary>
/// Turns a rule the database refused (unique, check, foreign key, or a trigger's RAISE EXCEPTION) into a client
/// answer. The aggregate and the handlers check the same rules first, so this is the last line: it is reached when
/// a code path skipped the check or two requests raced past it. The detail text is chosen here, never taken from
/// PostgreSQL, whose messages carry table and column names.
/// </summary>
public static class DatabaseRuleViolation
{
    // Friendly text for the constraints a client can plausibly hit. Anything else gets the generic text for its kind.
    private static readonly Dictionary<string, string> KnownConstraints = new(StringComparer.Ordinal)
    {
        ["CK_reward_tiers_never_oversold"] = "The reward tier has no slots left.",
        ["CK_campaign_contributions_ledger_sign_matches_kind"] = "The ledger entry's amount does not match its kind.",
    };

    /// <summary>Finds the PostgreSQL error behind an exception, whether EF wrapped it or raw SQL threw it directly.</summary>
    public static PostgresException? Find(Exception exception)
        => exception switch
        {
            PostgresException postgres => postgres,
            DbUpdateException { InnerException: PostgresException postgres } => postgres,
            _ => null,
        };

    /// <summary>The answer for a rule violation, or null when the exception is not one.</summary>
    public static (HttpStatusCode Status, string Title, string Detail)? Translate(Exception exception)
    {
        var postgres = Find(exception);
        if (postgres is null)
        {
            return null;
        }

        var named = postgres.ConstraintName is { } name && KnownConstraints.TryGetValue(name, out var text) ? text : null;

        return postgres.SqlState switch
        {
            PostgresErrorCodes.UniqueViolation =>
                (HttpStatusCode.Conflict, "Conflict", named ?? "A record with the same unique value already exists."),
            PostgresErrorCodes.CheckViolation =>
                (HttpStatusCode.Conflict, "Conflict", named ?? "The change breaks a rule the data must always follow."),
            PostgresErrorCodes.ForeignKeyViolation =>
                (HttpStatusCode.Conflict, "Conflict", named ?? "The change refers to a record that does not exist, or that is still in use."),
            // The append-only ledger trigger raises 55000; a bare RAISE EXCEPTION is P0001.
            PostgresErrorCodes.ObjectNotInPrerequisiteState or PostgresErrorCodes.RaiseException =>
                (HttpStatusCode.Conflict, "Conflict", "This record is protected and cannot be changed that way."),
            _ => null,
        };
    }
}

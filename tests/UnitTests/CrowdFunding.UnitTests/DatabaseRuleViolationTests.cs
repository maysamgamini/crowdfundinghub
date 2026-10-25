using System.Net;
using CrowdFunding.API.Observability;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CrowdFunding.UnitTests;

public sealed class DatabaseRuleViolationTests
{
    private static PostgresException Postgres(string sqlState, string? constraint = null)
        => new("raw database text with schema.table names", "ERROR", "ERROR", sqlState, constraintName: constraint);

    [Theory]
    [InlineData(PostgresErrorCodes.UniqueViolation)]
    [InlineData(PostgresErrorCodes.CheckViolation)]
    [InlineData(PostgresErrorCodes.ForeignKeyViolation)]
    [InlineData(PostgresErrorCodes.RaiseException)]
    [InlineData(PostgresErrorCodes.ObjectNotInPrerequisiteState)]
    public void EveryRuleViolation_IsAConflict_WithoutLeakingTheDatabaseText(string sqlState)
    {
        var result = DatabaseRuleViolation.Translate(Postgres(sqlState));

        Assert.NotNull(result);
        Assert.Equal(HttpStatusCode.Conflict, result!.Value.Status);
        Assert.DoesNotContain("schema.table", result.Value.Detail);
    }

    [Fact]
    public void ARuleViolationWrappedByEntityFramework_IsFoundToo()
    {
        var wrapped = new DbUpdateException("save failed", Postgres(PostgresErrorCodes.CheckViolation));

        Assert.Equal(HttpStatusCode.Conflict, DatabaseRuleViolation.Translate(wrapped)!.Value.Status);
    }

    [Fact]
    public void AKnownConstraint_GetsItsOwnSentence()
    {
        var result = DatabaseRuleViolation.Translate(Postgres(PostgresErrorCodes.CheckViolation, "CK_reward_tiers_never_oversold"));

        Assert.Equal("The reward tier has no slots left.", result!.Value.Detail);
    }

    [Fact]
    public void OtherDatabaseErrors_AreNotRuleViolations()
    {
        Assert.Null(DatabaseRuleViolation.Translate(Postgres(PostgresErrorCodes.SerializationFailure)));
        Assert.Null(DatabaseRuleViolation.Translate(new InvalidOperationException("nope")));
    }
}

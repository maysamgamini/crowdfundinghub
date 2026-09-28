using CrowdFunding.BuildingBlocks.Application.Exceptions;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.BuildingBlocks.Domain.Exceptions;
using CrowdFunding.BuildingBlocks.Infrastructure.ErrorHandling;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CrowdFunding.UnitTests;

public sealed class ExceptionClassifierTests
{
    [Fact]
    public void BusinessRuleViolation_ShouldMapTo400_WithItsOwnMessage()
    {
        var outcome = ExceptionClassifier.Classify(new BusinessRuleViolationException("Campaign title is required."));

        Assert.Equal(400, outcome.StatusCode);
        Assert.Equal("Campaign title is required.", outcome.Detail);
    }

    [Fact]
    public void DomainConflict_ShouldMapTo409_WithItsOwnMessage()
    {
        var outcome = ExceptionClassifier.Classify(new DomainConflictException("Only draft campaigns can be published."));

        Assert.Equal(409, outcome.StatusCode);
        Assert.Equal("Only draft campaigns can be published.", outcome.Detail);
    }

    [Theory]
    [MemberData(nameof(ApplicationExceptions))]
    public void ApplicationExceptions_ShouldMapToTheirStatusCodes(Exception exception, int expectedStatusCode)
    {
        Assert.Equal(expectedStatusCode, ExceptionClassifier.Classify(exception).StatusCode);
    }

    public static TheoryData<Exception, int> ApplicationExceptions() => new()
    {
        { new NotFoundException("Campaign was not found."), 404 },
        { new ResourceConflictException("Email already registered."), 409 },
        { new ConcurrencyConflictException("stale", new Exception()), 409 },
        { new UnauthenticatedException("not signed in"), 401 },
        { new ForbiddenAccessException("not the owner"), 403 },
    };

    [Theory]
    [MemberData(nameof(FrameworkExceptions))]
    public void FrameworkExceptions_ShouldBecomeGeneric500_WithoutLeakingTheirMessage(Exception exception)
    {
        var outcome = ExceptionClassifier.Classify(exception);

        Assert.Equal(500, outcome.StatusCode);
        Assert.True(outcome.IsServerFault);
        Assert.DoesNotContain("secret", outcome.Detail, StringComparison.OrdinalIgnoreCase);
    }

    public static TheoryData<Exception> FrameworkExceptions() => new()
    {
        new InvalidOperationException("The secret table campaigns.campaigns is missing."),
        new ArgumentException("secret connection string value"),
        new KeyNotFoundException("The given key 'secret' was not present in the dictionary."),
        new UnauthorizedAccessException("Access to the path '/secret' is denied."),
    };

    [Fact]
    public void PoolExhaustion_ShouldMapTo503()
    {
        var poolTimeout = new NpgsqlException("The connection pool has been exhausted.", new TimeoutException());

        Assert.Equal(503, ExceptionClassifier.Classify(poolTimeout).StatusCode);
    }

    [Theory]
    [InlineData(PostgresErrorCodes.TooManyConnections)]
    [InlineData(PostgresErrorCodes.CannotConnectNow)]
    public void ConnectionRefusedByPostgres_ShouldMapTo503_EvenWhenWrapped(string sqlState)
    {
        var refused = new PostgresException("refused", "FATAL", "FATAL", sqlState);
        var wrapped = new DbUpdateException("An error occurred while saving the entity changes.", refused);

        Assert.Equal(503, ExceptionClassifier.Classify(wrapped).StatusCode);
    }

    [Fact]
    public void UniqueViolation_ShouldMapTo409()
    {
        var unique = new PostgresException("duplicate key", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation);

        Assert.Equal(409, ExceptionClassifier.Classify(new DbUpdateException("save failed", unique)).StatusCode);
    }
}

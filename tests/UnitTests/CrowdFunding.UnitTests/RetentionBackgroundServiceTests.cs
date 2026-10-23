using CrowdFunding.BuildingBlocks.Application.Retention;
using CrowdFunding.BuildingBlocks.Infrastructure.Retention;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CrowdFunding.UnitTests;

public sealed class RetentionBackgroundServiceTests
{
    private sealed class FakeTask(string name, Func<Task<int>> run) : IRetentionTask
    {
        public string Name { get; } = name;

        public int Runs;

        public Task<int> RunAsync(IServiceProvider scopedServices, DateTime nowUtc, CancellationToken cancellationToken)
        {
            Runs++;
            return run();
        }
    }

    private static RetentionBackgroundService ServiceFor(params IRetentionTask[] tasks)
        => new(
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            tasks,
            Options.Create(new RetentionOptions()),
            NullLogger<RetentionBackgroundService>.Instance);

    [Fact]
    public async Task EveryTaskRuns_EvenWhenAnEarlierOneFails()
    {
        var failing = new FakeTask("failing", () => throw new InvalidOperationException("table is locked"));
        var healthy = new FakeTask("healthy", () => Task.FromResult(3));

        await ServiceFor(failing, healthy).RunOnceAsync(DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(1, failing.Runs);
        Assert.Equal(1, healthy.Runs);
    }

    [Fact]
    public void TheDefaults_AreAWeekForKeysAndTwoWeeksForTheInbox()
    {
        var options = new RetentionOptions();

        Assert.Equal(7, options.IdempotencyKeyDays);
        Assert.Equal(14, options.InboxDays);
        Assert.Equal(TimeSpan.FromHours(1), options.Interval);
    }
}

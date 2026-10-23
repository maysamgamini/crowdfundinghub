using CrowdFunding.BuildingBlocks.Infrastructure.Hosting;
using CrowdFunding.BuildingBlocks.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrowdFunding.UnitTests;

/// <summary>
/// An exception that escapes a background service's ExecuteAsync stops the whole host by default.
/// These tests pin down that a failed pass is logged and retried, and that only a stop request ends the loop.
/// </summary>
public sealed class GuardedLoopTests
{
    private sealed class CapturingLogger : ILogger
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (Entries)
            {
                Entries.Add((logLevel, formatter(state, exception), exception));
            }
        }
    }

    private sealed class CapturingLoggerProvider(CapturingLogger logger) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => logger;

        public void Dispose()
        {
        }
    }

    [Fact]
    public async Task AFailedIteration_IsLoggedAndTheLoopCarriesOn()
    {
        var logger = new CapturingLogger();
        using var stop = new CancellationTokenSource();
        var calls = 0;

        await GuardedLoop.RunAsync(ct =>
        {
            calls++;
            if (calls < 3)
            {
                throw new InvalidOperationException("database is down");
            }

            stop.Cancel();
            return Task.CompletedTask;
        }, TimeSpan.FromMilliseconds(5), logger, "test-loop", stop.Token);

        Assert.Equal(3, calls);
        Assert.Equal(2, logger.Entries.Count(e => e.Level == LogLevel.Error && e.Message.Contains("test-loop")));
        Assert.All(logger.Entries, e => Assert.IsType<InvalidOperationException>(e.Exception));
    }

    [Fact]
    public async Task ARequestToStop_EndsTheLoopWithoutAnError()
    {
        var logger = new CapturingLogger();
        using var stop = new CancellationTokenSource();

        await GuardedLoop.RunAsync(async ct =>
        {
            stop.Cancel();
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
        }, TimeSpan.FromMilliseconds(5), logger, "test-loop", stop.Token);

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task AStopRequestedWhileWaitingAfterAFailure_EndsTheLoop()
    {
        var logger = new CapturingLogger();
        using var stop = new CancellationTokenSource();

        var run = GuardedLoop.RunAsync(
            _ => throw new InvalidOperationException("boom"),
            TimeSpan.FromSeconds(30), logger, "test-loop", stop.Token);

        await Task.Delay(50);
        stop.Cancel();

        await run.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Single(logger.Entries);
    }

    [Fact]
    public async Task APersistentFailure_WaitsBetweenTries_InsteadOfSpinning()
    {
        var logger = new CapturingLogger();
        using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var calls = 0;

        await GuardedLoop.RunAsync(_ =>
        {
            Interlocked.Increment(ref calls);
            throw new InvalidOperationException("still down");
        }, TimeSpan.FromMilliseconds(50), logger, "test-loop", stop.Token);

        Assert.InRange(calls, 2, 10);
    }

    private sealed class FailingProcessor(IServiceProvider services)
        : ModuleOutboxProcessor<DbContext>(services, "no_such_table", TimeSpan.FromMilliseconds(20));

    [Fact]
    public async Task AnOutboxWorker_WhoseDatabaseKeepsFailing_StaysAliveAndStopsCleanly()
    {
        // No DbContext is registered, so every pass fails the moment it resolves one.
        var logger = new CapturingLogger();
        var services = new ServiceCollection()
            .AddLogging(builder => builder.AddProvider(new CapturingLoggerProvider(logger)))
            .BuildServiceProvider();
        using var worker = new FailingProcessor(services);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(300);

        Assert.False(worker.ExecuteTask!.IsCompleted, "A failing pass must not end the worker: an escaped exception would stop the host.");
        Assert.True(logger.Entries.Count(e => e.Level == LogLevel.Error) >= 2);

        await worker.StopAsync(CancellationToken.None);
        Assert.True(worker.ExecuteTask.IsCompletedSuccessfully);
    }
}

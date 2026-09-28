using System.Diagnostics;
using System.Text.Json;
using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Infrastructure.Observability;
using CrowdFunding.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// A self-contained, autonomous background worker draining exactly one module's outbox table.
/// Each module registers its own concrete subclass in its own Infrastructure DI extension
/// (<c>services.AddHostedService&lt;CampaignsOutboxBackgroundService&gt;()</c>) — the API host
/// never references a module's DbContext or outbox table name to make this run. That means when
/// a module is extracted into its own deployable process, its outbox engine moves with it
/// unchanged; there is no centralized worker to perform surgery on.
///
/// Publishes through <see cref="IMessageBus"/> rather than <see cref="IEventPublisher"/>
/// directly, so switching a module's <c>Messaging:Provider</c> from in-process to a distributed
/// broker changes nothing here. See docs/outbox-architecture.md for the SKIP LOCKED claim design.
///
/// Wake-up: a commit that writes outbox rows notifies this module's
/// <see cref="OutboxSignal{TDbContext}"/> (via <see cref="OutboxCommitSignalInterceptor{TDbContext}"/>),
/// so delivery normally starts milliseconds after the business transaction commits; the poll
/// interval is only the fallback sweep.
/// </summary>
public abstract class ModuleOutboxProcessor<TDbContext> : BackgroundService, IOutboxDispatcher
    where TDbContext : DbContext
{
    private static readonly ActivitySource ActivitySource = new(CrowdFundingTelemetry.OutboxActivitySourceName);
    private static readonly TimeSpan LockDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);
    private const int BatchSize = 20;
    private const int MaxAttempts = 5;

    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _pollInterval;
    private readonly string _workerId = $"{Environment.MachineName}:{Environment.ProcessId}:{typeof(TDbContext).Name}";

    protected ModuleOutboxProcessor(IServiceProvider serviceProvider, TimeSpan pollInterval)
    {
        _serviceProvider = serviceProvider;
        _pollInterval = pollInterval;
    }

    /// <summary>
    /// Drains the outbox whenever a commit signals new rows, and at least every poll interval as
    /// a fallback sweep (retries whose back-off has elapsed, rows from other replicas or
    /// processes, a signal missed during a restart). A full batch loops straight into the next
    /// one instead of waiting.
    /// </summary>
    /// <param name="stoppingToken">Signals host shutdown.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var signal = _serviceProvider.GetRequiredService<OutboxSignal<TDbContext>>();
        var logger = _serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());

        while (!stoppingToken.IsCancellationRequested)
        {
            var claimed = 0;

            try
            {
                claimed = await ProcessNextBatchAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                // A database outage must not kill the host (BackgroundService's default is to stop
                // the application on an unhandled exception). The rows stay Pending or their lease
                // expires, so the next pass picks them up.
                logger.LogError(exception, "Outbox pass for {DbContext} failed; retrying after the fallback interval.", typeof(TDbContext).Name);
            }

            if (claimed < BatchSize)
            {
                await signal.WaitAsync(_pollInterval, stoppingToken);
            }
        }
    }

    /// <inheritdoc/>
    public Task ProcessBatchAsync(CancellationToken cancellationToken) => ProcessNextBatchAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<bool> DrainAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (await ProcessNextBatchAsync(cancellationToken) > 0)
            {
                continue;
            }

            if (!await HasDueOrInFlightRowsAsync(cancellationToken))
            {
                return true;
            }

            // Another worker holds the remaining rows; give it a moment to finish publishing.
            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        }

        return false;
    }

    private async Task<bool> HasDueOrInFlightRowsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var nowUtc = DateTime.UtcNow;

        return await dbContext.Set<OutboxMessage>().AnyAsync(
            message => (message.Status == OutboxMessageStatus.Pending && message.ScheduledAtUtc <= nowUtc)
                       || message.Status == OutboxMessageStatus.Processing,
            cancellationToken);
    }

    /// <summary>
    /// Claims up to <see cref="BatchSize"/> due rows with <c>FOR UPDATE SKIP LOCKED</c>, publishes
    /// them, and deletes, reschedules or dead-letters each one.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows claimed.</returns>
    private async Task<int> ProcessNextBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var dbContext = services.GetRequiredService<TDbContext>();
        var messageBus = services.GetRequiredService<IMessageBus>();
        var eventTypeRegistry = services.GetRequiredService<EventTypeRegistry>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());

        var messages = await dbContext.ClaimPendingBatchAsync(_workerId, BatchSize, LockDuration, cancellationToken);

        if (messages.Count == 0)
        {
            return 0;
        }

        var nowUtc = DateTime.UtcNow;

        foreach (var message in messages)
        {
            if (!message.TryResolve(eventTypeRegistry, out var applicationEvent, out var failureReason))
            {
                logger.LogWarning(
                    "Routing unresolvable outbox message {MessageId} (EventType={EventType}, Version={Version}) to dead-letter: {Reason}",
                    message.Id, message.EventType, message.Version, failureReason);

                dbContext.Set<DeadLetterEvent>().Add(new DeadLetterEvent(
                    message.Id, message.EventType, message.Version, message.Payload, failureReason!, nowUtc));
                // TICKET-038: the active outbox table must hold only pending/in-flight rows —
                // leaving processed/dead-lettered rows in place forever is exactly the PostgreSQL
                // MVCC write-amplification trap (every UPDATE leaves a dead tuple; a row that's
                // never deleted just accumulates them under Status mutations too). The row's
                // permanent record now lives in DeadLetterEvents; deleting it here keeps the
                // outbox table bounded to only what a SKIP LOCKED claim actually needs to scan.
                dbContext.Set<OutboxMessage>().Remove(message);

                continue;
            }

            using var activity = StartActivityFromHeaders(message);

            try
            {
                await messageBus.PublishAsync(applicationEvent!, cancellationToken);
                // Delete-on-success (TICKET-038 Strategy A): no UPDATE-then-keep-forever: a
                // successfully published message has no further reason to exist in the active
                // table, so it's removed immediately rather than marked Processed and left behind.
                dbContext.Set<OutboxMessage>().Remove(message);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to publish outbox message {MessageId} (EventType={EventType}), attempt {Attempt}/{MaxAttempts}.",
                    message.Id, message.EventType, message.Attempts + 1, MaxAttempts);

                message.MarkFailed(exception.Message, MaxAttempts, RetryDelay, nowUtc);

                if (message.Status == OutboxMessageStatus.DeadLetter)
                {
                    dbContext.Set<DeadLetterEvent>().Add(new DeadLetterEvent(
                        message.Id, message.EventType, message.Version, message.Payload,
                        $"Exceeded {MaxAttempts} attempts. Last error: {exception.Message}", nowUtc));
                    dbContext.Set<OutboxMessage>().Remove(message);
                }
                // Below MaxAttempts, MarkFailed already pushed the row back to Pending with a
                // backed-off ScheduledAtUtc — it stays in the table because it's still active
                // work, not because it's being retained after completion.
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return messages.Count;
    }

    /// <summary>
    /// Restores the W3C trace context captured at the moment the HTTP request wrote this row, so
    /// the outbox dispatch span appears as a child of the originating request in a distributed
    /// trace instead of an orphaned root span. Falls back to a clean root activity (never throws)
    /// when the row carries no headers — e.g. an event queued from a CLI seeder or cron job with
    /// no ambient <see cref="Activity"/> at creation time.
    /// </summary>
    private static Activity? StartActivityFromHeaders(OutboxMessage message)
    {
        var parentContext = default(ActivityContext);

        try
        {
            var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(message.Headers);
            if (headers is not null
                && headers.TryGetValue("traceparent", out var traceparent)
                && !string.IsNullOrWhiteSpace(traceparent))
            {
                headers.TryGetValue("tracestate", out var tracestate);
                ActivityContext.TryParse(traceparent, tracestate, out parentContext);
            }
        }
        catch (JsonException)
        {
            // Malformed/legacy headers ("{}" default or pre-migration rows) — proceed with a
            // clean root activity rather than failing outbox processing over a tracing detail.
        }

        return ActivitySource.StartActivity(
            $"Outbox.Process {message.EventType}",
            ActivityKind.Consumer,
            parentContext);
    }
}

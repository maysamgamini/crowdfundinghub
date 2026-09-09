using System.Text;
using CrowdFunding.Modules.CampaignUpdates.Application.Abstractions.Persistence;
using CrowdFunding.Modules.CampaignUpdates.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CrowdFunding.Modules.CampaignUpdates.Infrastructure.Services;

/// <summary>
/// Dispatches due <see cref="Domain.Aggregates.WebhookDeliveryTask"/> rows to creators' external
/// endpoints from a background poll loop — never from an HTTP request thread, so a slow or
/// hostile target can never exhaust the API's own thread pool or block a backer's pledge. See
/// TICKET-035.
/// </summary>
public sealed class WebhookDispatcherBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 20;

    private readonly IServiceProvider _serviceProvider;

    public WebhookDispatcherBackgroundService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessBatchAsync(stoppingToken);
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    /// <summary>Exposed so integration tests can drive one dispatch pass deterministically
    /// instead of waiting on <see cref="PollInterval"/>, mirroring the generic outbox's own
    /// <c>ProcessBatchAsync</c> test hook.</summary>
    public async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var taskRepository = services.GetRequiredService<IWebhookDeliveryTaskRepository>();
        var subscriptionRepository = services.GetRequiredService<IWebhookSubscriptionRepository>();
        var httpClientFactory = services.GetRequiredService<IHttpClientFactory>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());

        var nowUtc = DateTime.UtcNow;
        var tasks = await taskRepository.ClaimDueBatchAsync(BatchSize, nowUtc, cancellationToken);

        foreach (var task in tasks)
        {
            var subscription = await subscriptionRepository.GetByIdAsync(task.SubscriptionId, cancellationToken);
            if (subscription is null || !subscription.IsActive)
            {
                // The subscription was disabled (or removed) since this task was queued —
                // nothing left to deliver to.
                task.MarkDelivered();
                await taskRepository.UpdateAsync(task, cancellationToken);
                continue;
            }

            try
            {
                using var httpClient = httpClientFactory.CreateClient(nameof(WebhookDispatcherBackgroundService));
                httpClient.Timeout = TimeSpan.FromSeconds(5);

                // Signed over the exact bytes sent — the payload built at enqueue time
                // (ContributionPaymentConfirmedActivityHandler etc.), sent verbatim as the
                // request body, never re-serialized here (a different JSON encoding of the same
                // logical object would still be signed correctly by the receiver, but would not
                // match this signature, which is computed over one specific byte sequence).
                using var request = new HttpRequestMessage(HttpMethod.Post, subscription.TargetUrl)
                {
                    Content = new StringContent(task.PayloadJson, Encoding.UTF8, "application/json"),
                };
                request.Headers.Add(
                    "X-CrowdFunding-Signature",
                    WebhookPayloadSigner.Sign(task.PayloadJson, subscription.SecretKey, DateTimeOffset.UtcNow));

                using var response = await httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

                task.MarkDelivered();
                subscription.RecordDeliverySuccess();
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Failed to deliver webhook task {TaskId} to subscription {SubscriptionId}, attempt {Attempt}.",
                    task.Id, task.SubscriptionId, task.Attempts + 1);

                task.MarkFailed(exception.Message, DateTime.UtcNow);
                subscription.RecordDeliveryFailure(maxConsecutiveFailures: 5);
            }

            await taskRepository.UpdateAsync(task, cancellationToken);
            await subscriptionRepository.UpdateAsync(subscription, cancellationToken);
        }
    }
}

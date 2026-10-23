using Microsoft.Extensions.Logging;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Hosting;

/// <summary>
/// Runs the body of a background service forever without letting one failure end it. In .NET an
/// exception that escapes <c>ExecuteAsync</c> faults the hosted service, and the default host behaviour
/// is then to stop the whole process, so one brief database outage inside a background pass would take
/// the API down with it. Here a failed iteration is logged and tried again after a pause; only a request
/// to stop ends the loop.
/// </summary>
public static class GuardedLoop
{
    /// <summary>
    /// Calls <paramref name="iteration"/> again and again. The iteration owns its own waiting (a timer tick
    /// or a signal), so a successful iteration is followed immediately by the next one. After a failure the
    /// loop waits <paramref name="delayAfterFailure"/> so a persistent fault does not spin.
    /// </summary>
    public static async Task RunAsync(
        Func<CancellationToken, Task> iteration,
        TimeSpan delayAfterFailure,
        ILogger logger,
        string loopName,
        CancellationToken stoppingToken)
    {
        ArgumentNullException.ThrowIfNull(iteration);
        ArgumentNullException.ThrowIfNull(logger);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await iteration(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Background loop {Loop} failed; it will try again in {DelaySeconds} s.",
                    loopName,
                    delayAfterFailure.TotalSeconds);

                try
                {
                    await Task.Delay(delayAfterFailure, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}

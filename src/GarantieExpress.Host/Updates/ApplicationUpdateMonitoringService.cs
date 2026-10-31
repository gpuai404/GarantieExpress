using GarantieExpress.Application.Common;

namespace GarantieExpress.Host.Updates;

internal sealed class ApplicationUpdateMonitoringService(
    ApplicationUpdateService updates,
    ILogger<ApplicationUpdateMonitoringService> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var nextDelay = updates.NextCheckInterval ?? RetryDelay;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(nextDelay, stoppingToken);
                var result = await updates.CheckAsync(stoppingToken);

                if (result is OperationResult.Succeeded
                    && updates.NextCheckInterval is { } configuredInterval)
                {
                    nextDelay = configuredInterval;
                    continue;
                }

                nextDelay = RetryDelay;
                logger.LogWarning(
                    "The next application update check will be retried in {RetryDelay}.",
                    RetryDelay);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogDebug("Periodic application update checks stopped.");
        }
    }
}

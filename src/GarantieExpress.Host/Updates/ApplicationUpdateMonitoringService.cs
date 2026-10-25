namespace GarantieExpress.Host.Updates;

internal sealed class ApplicationUpdateMonitoringService(
    ApplicationUpdateService updates,
    ILogger<ApplicationUpdateMonitoringService> logger) : BackgroundService
{
    internal static readonly TimeSpan CheckInterval = TimeSpan.FromHours(8);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await updates.CheckAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogDebug("Periodic application update checks stopped.");
        }
    }
}

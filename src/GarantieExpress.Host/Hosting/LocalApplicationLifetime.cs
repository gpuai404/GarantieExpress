using GarantieExpress.UI.Abstractions;

namespace GarantieExpress.Host.Hosting;

internal sealed class LocalApplicationLifetime(
    IHostApplicationLifetime hostLifetime,
    ILogger<LocalApplicationLifetime> logger) : ILocalApplicationLifetime
{
    private int _shutdownRequested;

    public void RequestShutdown()
    {
        if (Interlocked.Exchange(ref _shutdownRequested, 1) != 0) return;
        logger.LogInformation("Application shutdown requested from the local interface.");
        hostLifetime.StopApplication();
    }
}

using System.Diagnostics;

namespace GarantieExpress.Host.Hosting;

internal sealed class BrowserLauncher(ILogger<BrowserLauncher> logger)
{
    public void Open(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        try
        {
            using var process = Process.Start(CreateStartInfo(endpoint));
            if (process is null) logger.LogWarning("The default browser could not be started.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            logger.LogWarning(exception, "The default browser could not be started for {Endpoint}.", endpoint);
        }
    }

    private static ProcessStartInfo CreateStartInfo(Uri endpoint)
    {
        if (OperatingSystem.IsWindows())
            return new ProcessStartInfo(endpoint.AbsoluteUri) { UseShellExecute = true };

        var command = OperatingSystem.IsMacOS() ? "open" : "xdg-open";
        var startInfo = new ProcessStartInfo(command) { UseShellExecute = false };
        startInfo.ArgumentList.Add(endpoint.AbsoluteUri);
        return startInfo;
    }
}

using System.Text;
using GarantieExpress.Infrastructure.Storage;

namespace GarantieExpress.Host.Hosting;

internal sealed class SingleInstanceGuard(
    ApplicationPaths paths,
    TimeProvider timeProvider,
    ILogger<SingleInstanceGuard> logger) : IDisposable
{
    private static readonly TimeSpan EndpointPublicationTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan EndpointPollInterval = TimeSpan.FromMilliseconds(100);
    private readonly string _lockPath = Path.Combine(paths.DataDirectory, "application.lock");
    private FileStream? _lockStream;

    public async Task<SingleInstanceResult> AcquireAsync(
        CancellationToken cancellationToken = default)
    {
        if (_lockStream is not null)
            throw new InvalidOperationException("The application lock has already been acquired.");

        try
        {
            _lockStream = new FileStream(
                _lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.Read,
                bufferSize: 256,
                FileOptions.WriteThrough);
            _lockStream.SetLength(0);
            _lockStream.Flush(flushToDisk: true);
            return new SingleInstanceResult.Primary();
        }
        catch (IOException)
        {
            logger.LogInformation("Another GarantieExpress process owns the application lock.");
        }

        var endpoint = await ReadPublishedEndpointAsync(cancellationToken);
        return endpoint is null
            ? new SingleInstanceResult.Unavailable()
            : new SingleInstanceResult.Secondary(endpoint);
    }

    public void PublishEndpoint(Uri endpoint)
    {
        if (_lockStream is null)
            throw new InvalidOperationException(
                "The local endpoint can only be published by the primary application instance.");
        if (!LocalHostSettings.IsSupportedEndpoint(endpoint))
            throw new ArgumentException("The published endpoint must be a local HTTP endpoint.", nameof(endpoint));

        var bytes = Encoding.UTF8.GetBytes(endpoint.AbsoluteUri);
        _lockStream.SetLength(0);
        _lockStream.Position = 0;
        _lockStream.Write(bytes);
        _lockStream.Flush(flushToDisk: true);
    }

    private async Task<Uri?> ReadPublishedEndpointAsync(
        CancellationToken cancellationToken)
    {
        var startedAt = timeProvider.GetTimestamp();
        while (timeProvider.GetElapsedTime(startedAt) < EndpointPublicationTimeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var stream = new FileStream(
                    _lockPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    bufferSize: 256,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                var value = await reader.ReadToEndAsync(cancellationToken);
                if (Uri.TryCreate(value, UriKind.Absolute, out var endpoint)
                    && LocalHostSettings.IsSupportedEndpoint(endpoint))
                {
                    return endpoint;
                }
            }
            catch (IOException)
            {
                // The primary instance can still be publishing its endpoint.
            }

            await Task.Delay(EndpointPollInterval, timeProvider, cancellationToken);
        }

        logger.LogWarning(
            "The primary GarantieExpress process did not publish its endpoint within {Timeout}.",
            EndpointPublicationTimeout);
        return null;
    }

    public void Dispose()
    {
        _lockStream?.Dispose();
        _lockStream = null;
    }
}

internal abstract record SingleInstanceResult
{
    private SingleInstanceResult() { }

    public sealed record Primary : SingleInstanceResult;

    public sealed record Secondary(Uri Endpoint) : SingleInstanceResult;

    public sealed record Unavailable : SingleInstanceResult;
}

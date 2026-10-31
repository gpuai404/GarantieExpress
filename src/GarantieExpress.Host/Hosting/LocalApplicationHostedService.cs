using GarantieExpress.Application.Initialization;
using GarantieExpress.Host.Updates;
using GarantieExpress.Infrastructure.Persistence.Sqlite.Initialization;
using GarantieExpress.Infrastructure.Storage;

namespace GarantieExpress.Host.Hosting;

internal sealed class LocalApplicationHostedService(
    IServiceScopeFactory scopeFactory,
    IHostApplicationLifetime applicationLifetime,
    SingleInstanceGuard instanceGuard,
    BrowserLauncher browser,
    LocalHostSettings settings,
    DatabaseOperationGate databaseOperations,
    ApplicationUpdateService updates,
    ILogger<LocalApplicationHostedService> logger) : IHostedService, IDisposable
{
    private CancellationTokenRegistration _startedRegistration;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var initialData = scope.ServiceProvider
            .GetRequiredService<InitialApplicationDataFactory>()
            .Create();
        await scope.ServiceProvider
            .GetRequiredService<DatabaseInitializer>()
            .InitializeAsync(initialData, cancellationToken);

        await updates.CheckAsync(cancellationToken);

        _startedRegistration = applicationLifetime.ApplicationStarted.Register(OnApplicationStarted);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await databaseOperations.StopAcceptingAndDrainAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Timed out while waiting for active local database operations to finish.");
        }
    }

    public void Dispose() => _startedRegistration.Dispose();

    private void OnApplicationStarted()
    {
        instanceGuard.PublishEndpoint(settings.Endpoint);
        if (settings.LaunchBrowser) browser.Open(settings.Endpoint);
    }
}

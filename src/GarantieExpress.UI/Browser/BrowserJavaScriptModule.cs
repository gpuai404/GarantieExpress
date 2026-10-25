using Microsoft.JSInterop;

namespace GarantieExpress.UI.Browser;

internal sealed class BrowserJavaScriptModule(IJSRuntime jsRuntime, string modulePath) : IAsyncDisposable
{
    private readonly SemaphoreSlim _importLock = new(1, 1);
    private IJSObjectReference? _module;
    private bool _disposed;

    public async ValueTask<IJSObjectReference> GetAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_module is not null) return _module;

        await _importLock.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _module ??= await jsRuntime.InvokeAsync<IJSObjectReference>(
                "import",
                cancellationToken,
                modulePath);
            return _module;
        }
        finally
        {
            _importLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _importLock.WaitAsync();
        try
        {
            _disposed = true;
            if (_module is null) return;

            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The circuit is already gone; there is no browser module left to release.
            }
            _module = null;
        }
        finally
        {
            _importLock.Release();
        }
    }
}

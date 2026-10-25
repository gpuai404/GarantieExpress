using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using GarantieExpress.Application.Common;
using GarantieExpress.UI.Abstractions;

namespace GarantieExpress.UI.Browser;

internal sealed class BrowserLanguagePreferenceStore(
    IJSRuntime jsRuntime,
    ILogger<BrowserLanguagePreferenceStore> logger) : ILanguagePreferenceStore, IAsyncDisposable
{
    private readonly BrowserJavaScriptModule _module = new(
        jsRuntime,
        "./_content/GarantieExpress.UI/js/localization.js");

    public async Task<OperationResult<string?>> GetLanguageAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var module = await _module.GetAsync(cancellationToken);
            await module.InvokeVoidAsync("bootstrapLanguage", cancellationToken);
            return OperationResult<string?>.Success(
                await module.InvokeAsync<string>("getLanguage", cancellationToken));
        }
        catch (Exception exception) when (exception is JSException or JSDisconnectedException)
        {
            logger.LogWarning(exception, "Could not read the language preference.");
            return OperationResult<string?>.Failure(new OperationError(
                "Language.PreferenceReadFailed",
                "Could not read the language preference."));
        }
    }

    public async Task<OperationResult> ApplyLanguageAsync(
        string language,
        bool persist,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await (await _module.GetAsync(cancellationToken))
                .InvokeVoidAsync("applyLanguage", cancellationToken, language, persist);
            return OperationResult.Success();
        }
        catch (Exception exception) when (exception is JSException or JSDisconnectedException)
        {
            logger.LogWarning(exception, "Could not apply the language preference.");
            return OperationResult.Failure(new OperationError(
                "Language.PreferenceApplyFailed",
                "Could not apply the language preference."));
        }
    }

    public ValueTask DisposeAsync() => _module.DisposeAsync();
}

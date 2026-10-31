using Microsoft.JSInterop;
using GarantieExpress.Application.Localization;
using GarantieExpress.Application.Common;
using GarantieExpress.UI.Abstractions;

namespace GarantieExpress.UI.Browser;

internal sealed class BrowserPrintService(
    IJSRuntime jsRuntime,
    ILocalizationService localization) : IPrintService, IAsyncDisposable
{
    private const string WarrantyTargetSelector = ".warranty-print-target .warranty-document";
    private readonly BrowserJavaScriptModule _module = new(
        jsRuntime,
        "./_content/GarantieExpress.UI/js/print.js");

    public async Task<OperationResult<bool>> FitsPageAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var module = await _module.GetAsync(cancellationToken);
            var fits = await module.InvokeAsync<bool>(
                "warrantyFitsPage",
                cancellationToken,
                WarrantyTargetSelector);
            return OperationResult<bool>.Success(fits);
        }
        catch (Exception exception) when (exception is JSException or JSDisconnectedException)
        {
            return OperationResult<bool>.Failure(CreateUnavailableError());
        }
    }

    public async Task<OperationResult> PrintAsync(
        double widthMm,
        double heightMm,
        bool inkSaver,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var module = await _module.GetAsync(cancellationToken);
            await module.InvokeVoidAsync(
                "printWarranty",
                cancellationToken,
                widthMm,
                heightMm,
                inkSaver,
                WarrantyTargetSelector);
            return OperationResult.Success();
        }
        catch (Exception exception) when (exception is JSException or JSDisconnectedException)
        {
            return OperationResult.Failure(CreateUnavailableError());
        }
    }

    private OperationError CreateUnavailableError() => new(
        "Print.Unavailable",
        localization["Issue_PrintFailed"]);

    public ValueTask DisposeAsync() => _module.DisposeAsync();
}

using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using GarantieExpress.Application.Localization;
using GarantieExpress.Application.Common;
using GarantieExpress.Application.Warranties.Issue;
using GarantieExpress.Domain;
using GarantieExpress.UI.Abstractions;

namespace GarantieExpress.UI.Browser;

internal sealed class BrowserIssuanceRecoveryStore(
    IJSRuntime jsRuntime,
    ILogger<BrowserIssuanceRecoveryStore> logger,
    ILocalizationService localization) : IIssuanceRecoveryStore, IAsyncDisposable
{
    private readonly BrowserJavaScriptModule _module = new(
        jsRuntime,
        "./_content/GarantieExpress.UI/js/issuance-recovery.js");

    public async Task<OperationResult<WarrantyIssueRequest?>> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var stored = await InvokeAsync<WarrantyIssueRequestPayload?>("get", cancellationToken);
        if (stored is OperationResult<WarrantyIssueRequestPayload?>.Failed failed)
            return OperationResult<WarrantyIssueRequest?>.Failure(failed.Details);

        var payload = ((OperationResult<WarrantyIssueRequestPayload?>.Succeeded)stored).Value;
        if (payload is null) return OperationResult<WarrantyIssueRequest?>.Success(null);

        var request = WarrantyIssueRequest.Restore(payload.Id, payload.RequestedAt);
        if (request is DomainResult<WarrantyIssueRequest, WarrantyIssueRequestError>.Succeeded succeeded)
            return OperationResult<WarrantyIssueRequest?>.Success(succeeded.Value);

        var repaired = await InvokeVoidAsync(
            "clear",
            cancellationToken,
            payload.Id ?? string.Empty);
        return repaired is OperationResult.Failed repairFailure
            ? OperationResult<WarrantyIssueRequest?>.Failure(repairFailure.Details)
            : OperationResult<WarrantyIssueRequest?>.Success(null);
    }

    public async Task<OperationResult> SaveAsync(
        WarrantyIssueRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await InvokeAsync<bool>("save", cancellationToken, request);
        return result.Match(
            saved => saved ? OperationResult.Success() : OperationResult.Failure(new OperationError(
                "Issuance.RecoveryConflict", localization["Issue_RecoveryConflict"])),
            OperationResult.Failure);
    }

    public Task<OperationResult> ClearAsync(string requestId, CancellationToken cancellationToken = default) =>
        InvokeVoidAsync("clear", cancellationToken, requestId);

    private async Task<OperationResult<T>> InvokeAsync<T>(
        string identifier, CancellationToken cancellationToken, params object[] arguments)
    {
        try
        {
            return OperationResult<T>.Success(
                await (await _module.GetAsync(cancellationToken))
                    .InvokeAsync<T>(identifier, cancellationToken, arguments));
        }
        catch (Exception exception) when (exception is JSException or JSDisconnectedException)
        {
            logger.LogWarning(exception, "Browser issuance recovery storage is unavailable.");
            return OperationResult<T>.Failure(Unavailable());
        }
    }

    private async Task<OperationResult> InvokeVoidAsync(
        string identifier,
        CancellationToken cancellationToken,
        params object[] arguments)
    {
        try
        {
            await (await _module.GetAsync(cancellationToken))
                .InvokeVoidAsync(identifier, cancellationToken, arguments);
            return OperationResult.Success();
        }
        catch (Exception exception) when (exception is JSException or JSDisconnectedException)
        {
            logger.LogWarning(exception, "Browser issuance recovery storage is unavailable.");
            return OperationResult.Failure(Unavailable());
        }
    }

    private OperationError Unavailable() => new(
        "Issuance.RecoveryStoreUnavailable",
        localization["Issue_RecoveryUnavailable"]);

    public ValueTask DisposeAsync() => _module.DisposeAsync();

    private sealed record WarrantyIssueRequestPayload(
        string? Id,
        DateTimeOffset RequestedAt);
}

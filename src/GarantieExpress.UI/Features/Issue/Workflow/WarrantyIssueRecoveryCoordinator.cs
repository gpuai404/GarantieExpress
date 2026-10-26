using GarantieExpress.Application.Common;
using GarantieExpress.Application.Warranties.Issue;
using GarantieExpress.Domain;
using GarantieExpress.UI.Abstractions;

namespace GarantieExpress.UI.Features.Issue.Workflow;

public sealed class WarrantyIssueRecoveryCoordinator(
    WarrantyIssueRecoveryService warrantyIssues,
    IIssuanceRecoveryStore recoveryStore)
{
    private WarrantyIssueRequest? _pendingRequest;
    private string? _cleanupRequestId;

    public bool IsReady { get; private set; }
    public bool HasPendingRequest => _pendingRequest is not null;

    public async Task<OperationResult<WarrantyIssueResult?>>
        InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_cleanupRequestId is not null)
        {
            var cleanup = await ClearRecoveryMarkerAsync(
                _cleanupRequestId,
                cancellationToken);
            return cleanup is OperationResult.Failed failedCleanup
                ? Failure(failedCleanup.Details)
                : Success(null);
        }

        var stored = await recoveryStore.GetAsync(cancellationToken);
        if (stored is OperationResult<WarrantyIssueRequest?>.Failed failed)
            return Failure(failed.Details);

        IsReady = true;
        _pendingRequest = ((OperationResult<WarrantyIssueRequest?>.Succeeded)stored).Value;
        if (_pendingRequest is null)
            return Success(null);

        return await RecoverPendingAsync(cancellationToken);
    }

    public async Task<OperationResult<WarrantyIssueRequest>> PrepareAsync(
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken = default)
    {
        if (!IsReady)
        {
            return OperationResult<WarrantyIssueRequest>.Failure(new OperationError(
                "Issuance.RecoveryNotInitialized",
                "Issuance recovery must be initialized before issuing a warranty."));
        }

        _pendingRequest ??= WarrantyIssueRequest.Create(requestedAt);
        var saved = await recoveryStore.SaveAsync(_pendingRequest, cancellationToken);
        if (saved is OperationResult.Failed failed)
        {
            IsReady = false;
            return OperationResult<WarrantyIssueRequest>.Failure(failed.Details);
        }

        return OperationResult<WarrantyIssueRequest>.Success(_pendingRequest);
    }

    public Task<OperationResult<WarrantyIssueResult?>>
        RecoverPendingAsync(CancellationToken cancellationToken = default) =>
        _pendingRequest is null
            ? Task.FromResult(Success(null))
            : warrantyIssues.RecoverAsync(_pendingRequest.Id, cancellationToken);

    public async Task<OperationResult> CompleteAsync(
        CancellationToken cancellationToken = default)
    {
        var requestId = _pendingRequest?.Id ?? _cleanupRequestId;
        if (requestId is null) return OperationResult.Success();

        // Once an issuance has been committed, its request must never remain eligible
        // to take ownership of a newly edited form. Keep failed cleanup separate so it
        // can be retried before the next issuance.
        _pendingRequest = null;
        _cleanupRequestId = requestId;
        return await ClearRecoveryMarkerAsync(requestId, cancellationToken);
    }

    private async Task<OperationResult> ClearRecoveryMarkerAsync(
        string requestId,
        CancellationToken cancellationToken)
    {
        var result = await recoveryStore.ClearAsync(requestId, cancellationToken);
        IsReady = result is OperationResult.Succeeded;
        if (IsReady) _cleanupRequestId = null;
        return result;
    }

    private static OperationResult<WarrantyIssueResult?> Success(
        WarrantyIssueResult? value) =>
        OperationResult<WarrantyIssueResult?>.Success(value);

    private static OperationResult<WarrantyIssueResult?> Failure(
        OperationError error) =>
        OperationResult<WarrantyIssueResult?>.Failure(error);
}

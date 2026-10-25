using Microsoft.Extensions.Logging;
using GarantieExpress.Application.Localization;
using GarantieExpress.Application.Common;
using GarantieExpress.Application.Warranties.Drafts;

namespace GarantieExpress.UI.Features.Issue.Workflow;

public sealed class IssueDraftCoordinator(
    WarrantyDraftService drafts,
    ILocalizationService localization,
    ILogger<IssueDraftCoordinator> logger) : IAsyncDisposable
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(600);

    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private CancellationTokenSource? _scheduledSave;
    private Task _scheduledTask = Task.CompletedTask;
    private Task? _disposeTask;
    private DraftWarranty? _pendingDraft;
    private long _draftVersion;
    private bool _disposed;

    public event Action? StateChanged;

    public bool Failed { get; private set; }
    public string Status { get; private set; } = string.Empty;

    public void Initialize(DraftWarranty? draft, OperationError? warning)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (draft is not null)
            _draftVersion = draft.Version;

        Failed = warning is not null;
        Status = warning?.Message ?? localization[
            draft is null ? "Issue_AutosaveReady" : "Issue_DraftRestored"];
        NotifyStateChanged();
    }

    public DraftWarranty Track(DraftWarranty draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return draft with { Version = _draftVersion };
    }

    public void Schedule(DraftWarranty draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (_disposed) return;

        CancelScheduledSave();
        _scheduledSave = new CancellationTokenSource();
        _pendingDraft = draft;
        Failed = false;
        Status = localization["Issue_DraftPending"];
        NotifyStateChanged();
        _scheduledTask = SaveAfterDelayAsync(_scheduledTask, _scheduledSave.Token);
    }

    public async Task<OperationResult> FlushAsync()
    {
        CancelScheduledSave();
        await _scheduledTask;
        while (_pendingDraft is not null)
        {
            var result = await SavePendingAsync();
            if (result is OperationResult.Failed) return result;
        }

        return OperationResult.Success();
    }

    public async Task<OperationResult> ClearAsync(
        CancellationToken cancellationToken = default)
    {
        CancelScheduledSave();
        await _saveGate.WaitAsync(cancellationToken);
        try
        {
            var result = await drafts.ClearAsync(
                _draftVersion,
                cancellationToken);
            if (result is OperationResult.Failed failed)
            {
                Failed = true;
                Status = failed.Error;
                return result;
            }

            ResetIdentity();
            _pendingDraft = null;
            Failed = false;
            Status = localization["Issue_DraftCleared"];
            return result;
        }
        finally
        {
            _saveGate.Release();
            NotifyStateChanged();
        }
    }

    public async Task WaitForIdleAsync(CancellationToken cancellationToken = default)
    {
        CancelScheduledSave();
        await _saveGate.WaitAsync(cancellationToken);
        _saveGate.Release();
    }

    public void CompleteIssue()
    {
        CancelScheduledSave();
        _pendingDraft = null;
        ResetIdentity();
        Failed = false;
        Status = localization["Issue_DraftClearedAfterIssue"];
        NotifyStateChanged();
    }

    private async Task SaveAfterDelayAsync(
        Task previousSave,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.WhenAll(previousSave, Task.Delay(SaveDelay, cancellationToken));
            await SavePendingAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // A newer edit or a terminal workflow operation superseded this save.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "An unexpected error interrupted draft autosave.");
            Failed = true;
            Status = localization["Issue_DraftError"];
            NotifyStateChanged();
        }
    }

    private async Task<OperationResult> SavePendingAsync(
        CancellationToken cancellationToken = default)
    {
        await _saveGate.WaitAsync(cancellationToken);
        try
        {
            var draft = _pendingDraft;
            if (draft is null) return OperationResult.Success();

            Status = localization["Issue_DraftSaving"];
            NotifyStateChanged();

            // Cancelling a debounce must not interrupt a write that has already started.
            var result = await drafts.SaveAsync(Track(draft), CancellationToken.None);
            Failed = result is OperationResult<DraftWarranty>.Failed;
            if (result is OperationResult<DraftWarranty>.Succeeded saved)
            {
                _draftVersion = saved.Value.Version;
                if (ReferenceEquals(_pendingDraft, draft)) _pendingDraft = null;
                Status = localization[_pendingDraft is null ? "Issue_DraftSaved" : "Issue_DraftPending"];
                return OperationResult.Success();
            }

            var failed = (OperationResult<DraftWarranty>.Failed)result;
            Status = failed.Error;
            return OperationResult.Failure(failed.Details);
        }
        finally
        {
            _saveGate.Release();
            if (!_disposed) NotifyStateChanged();
        }
    }

    private void ResetIdentity()
    {
        _draftVersion = 0;
    }

    private void CancelScheduledSave()
    {
        _scheduledSave?.Cancel();
        _scheduledSave?.Dispose();
        _scheduledSave = null;
    }

    private void NotifyStateChanged()
    {
        if (!_disposed) StateChanged?.Invoke();
    }

    public ValueTask DisposeAsync() => new(_disposeTask ??= DisposeCoreAsync());

    private async Task DisposeCoreAsync()
    {
        _disposed = true;
        var result = await FlushAsync();
        if (result is OperationResult.Failed failed)
            logger.LogWarning("Could not save the draft while leaving the issue page: {Code}", failed.Details.Code);
    }
}

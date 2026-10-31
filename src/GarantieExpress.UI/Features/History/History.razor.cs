using Microsoft.AspNetCore.Components;
using GarantieExpress.Application.Localization;
using GarantieExpress.Application.Common;
using GarantieExpress.Application.Settings;
using GarantieExpress.Application.Warranties.Drafts;
using GarantieExpress.Application.Warranties.History;
using GarantieExpress.UI.Components.Warranty.Presentation;
using GarantieExpress.UI.Components.Warranty;
using GarantieExpress.UI.Components.Warranty.Templates;
using GarantieExpress.Domain;
using GarantieExpress.UI.Localization;

namespace GarantieExpress.UI.Features.History;

public partial class History
{
    private const int HistoryPageSize = WarrantyHistoryPageRequest.DefaultPageSize;

    [Inject] private ILocalizationService Localization { get; set; } = null!;
    [Inject] private WarrantyHistoryService WarrantyHistory { get; set; } = null!;
    [Inject] private WarrantyDisplayModelBuilder ViewModelBuilder { get; set; } = null!;
    [Inject] private TemplateRegistry Templates { get; set; } = null!;
    [Inject] private WarrantyPrintCoordinator Printer { get; set; } = null!;
    [Inject] private StoreSettingsService Settings { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    [Inject] private TimeProvider TimeProvider { get; set; } = null!;

    private enum HistoryAction { Void, Delete, Duplicate }

    private List<WarrantyRecord> _warranties = [];
    private WarrantyRecord? _selectedWarranty;
    private WarrantySnapshot? _selectedSnapshot;
    private WarrantyDisplayModel? _selectedModel;
    private WarrantyTemplate? _selectedTemplate;
    private WarrantyRecord? _pendingWarranty;
    private DraftWarranty? _draftToReplace;
    private HistoryAction _pendingAction;
    private bool _loading = true;
    private bool _printing;
    private bool _busy;
    private bool _inkSaver;
    private bool _actionFailed;
    private string _search = string.Empty;
    private long _totalCount;
    private int _pageIndex;
    private string? _loadError;
    private string? _actionMessage;

    private long PageCount =>
        _totalCount / HistoryPageSize + (_totalCount % HistoryPageSize == 0 ? 0 : 1);

    private string ConfirmationTitle => Localization[_pendingAction switch
    {
        HistoryAction.Delete => "History_DeleteConfirmTitle",
        HistoryAction.Duplicate => "History_ReplaceDraftTitle",
        _ => "History_VoidConfirmTitle"
    }];

    private string ConfirmationText => Localization.Format(_pendingAction switch
    {
        HistoryAction.Delete => "History_DeleteConfirmText",
        HistoryAction.Duplicate => "History_ReplaceDraftText",
        _ => "History_VoidConfirmText"
    }, _pendingWarranty?.Number ?? string.Empty);

    protected override async Task OnInitializedAsync()
    {
        var settingsTask = Settings.LoadAsync();
        var historyTask = LoadAsync();
        await Task.WhenAll(settingsTask, historyTask);
        var settings = await settingsTask;
        if (settings is OperationResult<StoreSettings>.Succeeded settingsSucceeded)
            _inkSaver = settingsSucceeded.Value.InkSaver;
        _loading = false;
    }

    private async Task LoadAsync()
    {
        var result = await WarrantyHistory.SearchAsync(_search, _pageIndex);
        if (result is OperationResult<WarrantyHistoryPage>.Failed failed)
        {
            _loadError = failed.Error;
            return;
        }

        var page = ((OperationResult<WarrantyHistoryPage>.Succeeded)result).Value;
        _warranties = page.Warranties.ToList();
        _totalCount = page.TotalCount;
        _pageIndex = page.PageIndex;
        _loadError = null;
    }

    private async Task ReloadAsync()
    {
        await LoadAsync();
        StateHasChanged();
    }

    private async Task SearchAsync(ChangeEventArgs args)
    {
        _search = args.Value?.ToString() ?? string.Empty;
        _pageIndex = 0;
        await LoadAsync();
    }

    private async Task ChangePageAsync(int pageIndex)
    {
        if (pageIndex < 0 || (long)pageIndex * HistoryPageSize >= _totalCount) return;
        _pageIndex = pageIndex;
        await LoadAsync();
    }

    private async Task OpenAsync(WarrantyRecord warranty)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            SetActionMessage(null, false);
            var snapshotResult = await WarrantyHistory.GetSnapshotAsync(warranty);
            if (snapshotResult is not OperationResult<WarrantySnapshot?>.Succeeded snapshotSucceeded
                || snapshotSucceeded.Value is null)
            {
                SetActionMessage(snapshotResult is OperationResult<WarrantySnapshot?>.Failed failed
                    ? failed.Error
                    : Localization["History_SnapshotMissing"], true);
                return;
            }

            _selectedWarranty = warranty;
            _selectedSnapshot = snapshotSucceeded.Value;
            _selectedTemplate = Templates.Get(warranty.LastTemplateKey);
            RebuildSelectedModel();
        }
        finally
        {
            _busy = false;
        }
    }

    private void ChangeDesign(ChangeEventArgs args)
    {
        _selectedTemplate = Templates.WithDesign(
            _selectedTemplate?.Key,
            args.Value?.ToString());
        RebuildSelectedModel();
    }

    private void ChangePageSize(ChangeEventArgs args)
    {
        _selectedTemplate = Templates.WithPageSize(
            _selectedTemplate?.Key,
            args.Value?.ToString());
        RebuildSelectedModel();
    }

    private void RebuildSelectedModel()
    {
        if (_selectedWarranty is null || _selectedSnapshot is null || _selectedTemplate is null) return;
        _selectedModel = ViewModelBuilder.Build(
            _selectedWarranty,
            _selectedSnapshot,
            _selectedTemplate.Illustration);
    }

    private async Task PrintSelectedAsync()
    {
        if (_printing || _selectedTemplate is null) return;
        _printing = true;
        try
        {
            await InvokeAsync(StateHasChanged);
            var printResult = await Printer.PrintAsync(_selectedTemplate, _inkSaver);
            if (printResult is OperationResult.Failed failedPrint)
                SetActionMessage(failedPrint.Error, true);
        }
        finally
        {
            _printing = false;
        }
    }

    private async Task DuplicateAsync(WarrantyRecord warranty)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var currentDraft = await WarrantyHistory.GetDraftAsync();
            if (currentDraft is OperationResult<DraftWarranty?>.Failed failed)
            {
                SetActionMessage(failed.Error, true);
                return;
            }

            _draftToReplace = ((OperationResult<DraftWarranty?>.Succeeded)currentDraft).Value;
            if (_draftToReplace is not null)
            {
                AskConfirmation(warranty, HistoryAction.Duplicate);
                return;
            }

            var result = await WarrantyHistory.DuplicateAsDraftAsync(warranty);
            if (result is OperationResult<DraftWarranty>.Succeeded) Navigation.NavigateTo("/uitgeven");
            else SetActionMessage(((OperationResult<DraftWarranty>.Failed)result).Error, true);
        }
        finally
        {
            _busy = false;
        }
    }

    private void AskDelete(WarrantyRecord warranty) =>
        AskConfirmation(warranty, HistoryAction.Delete);

    private void AskVoid(WarrantyRecord warranty) =>
        AskConfirmation(warranty, HistoryAction.Void);

    private void OnInkSaverChanged(bool value) => _inkSaver = value;

    private void AskConfirmation(WarrantyRecord warranty, HistoryAction action)
    {
        _pendingWarranty = warranty;
        _pendingAction = action;
    }

    private void CancelConfirmation()
    {
        if (_busy) return;
        _pendingWarranty = null;
        _draftToReplace = null;
    }

    private async Task ConfirmActionAsync()
    {
        if (_pendingWarranty is null || _busy) return;
        _busy = true;
        try
        {
            var warranty = _pendingWarranty;
            if (_pendingAction == HistoryAction.Duplicate)
            {
                var duplicated = await WarrantyHistory.DuplicateAsDraftAsync(warranty, _draftToReplace);
                if (duplicated is OperationResult<DraftWarranty>.Succeeded)
                    Navigation.NavigateTo("/uitgeven");
                else SetActionMessage(((OperationResult<DraftWarranty>.Failed)duplicated).Error, true);
                return;
            }

            var result = _pendingAction == HistoryAction.Delete
                ? await WarrantyHistory.DeleteAsync(warranty)
                : await VoidAsync(warranty);
            if (result is OperationResult.Succeeded)
            {
                SetActionMessage(
                    _pendingAction == HistoryAction.Delete
                        ? Localization["History_Deleted"]
                        : Localization["History_Voided"],
                    false);
                if (_pendingAction == HistoryAction.Delete && _selectedWarranty?.Number == warranty.Number)
                {
                    _selectedWarranty = null;
                    _selectedSnapshot = null;
                    _selectedModel = null;
                    _selectedTemplate = null;
                }
                await ReloadAsync();
            }
            else SetActionMessage(((OperationResult.Failed)result).Error, true);
        }
        finally
        {
            _pendingWarranty = null;
            _draftToReplace = null;
            _busy = false;
        }
    }

    private async Task<OperationResult> VoidAsync(WarrantyRecord warranty)
    {
        var result = await WarrantyHistory.VoidAsync(warranty);
        return result is OperationResult<WarrantyRecord>.Succeeded
            ? OperationResult.Success()
            : OperationResult.Failure(((OperationResult<WarrantyRecord>.Failed)result).Details);
    }

    private WarrantyEffectiveStatus EffectiveStatus(WarrantyRecord warranty) =>
        WarrantyStatusResolver.Resolve(
            warranty.Status,
            warranty.ValidUntil,
            DateOnly.FromDateTime(TimeProvider.GetLocalNow().DateTime));

    private string StatusText(WarrantyRecord warranty) => Localization[EffectiveStatus(warranty) switch
    {
        WarrantyEffectiveStatus.Voided => "History_StatusVoided",
        WarrantyEffectiveStatus.Expired => "History_StatusExpired",
        _ => "History_StatusActive"
    }];

    private void SetActionMessage(string? message, bool failed)
    {
        _actionMessage = message;
        _actionFailed = failed;
    }
}

using Microsoft.AspNetCore.Components.Forms;
using GarantieExpress.Application.Localization;
using GarantieExpress.Application.Common;
using GarantieExpress.Application.Categories;
using GarantieExpress.Application.Warranties.Drafts;
using GarantieExpress.Application.Warranties.Issue;
using GarantieExpress.Domain;
using GarantieExpress.UI.Components.Warranty.Presentation;
using GarantieExpress.UI.Components.Warranty;
using GarantieExpress.UI.Components.Warranty.Templates;
using GarantieExpress.UI.Features.Issue.Models;

namespace GarantieExpress.UI.Features.Issue.Workflow;

public enum IssueWorkflowLoadState
{
    Loading,
    LoadFailed,
    Editing
}

internal sealed class IssueEditingSession(
    StoreSettings settings,
    IReadOnlyList<Category> categories,
    IReadOnlyList<Product> products,
    IssueWarrantyForm form)
{
    public StoreSettings Settings { get; } = settings;
    public CategorySelection Catalog { get; } = new(categories);
    public IReadOnlyList<Category> Categories => Catalog.Items;
    public IReadOnlyList<Product> Products { get; } = Array.AsReadOnly(products.ToArray());
    public IssueWarrantyForm Form { get; private set; } = form;
    public EditContext EditContext { get; private set; } = new(form);

    public void ReplaceForm(IssueWarrantyForm replacement)
    {
        Form = replacement;
        EditContext = new EditContext(replacement);
    }
}

public sealed class IssueWorkflow(
    IssueWorkspaceService workspaceService,
    WarrantyIssueService issuer,
    IssueDraftCoordinator drafts,
    IssueRenderCoordinator rendering,
    WarrantyIssueRecoveryCoordinator issueRecovery,
    WarrantyDisplayModelBuilder displayModels,
    TemplateRegistry templates,
    WarrantyPrintCoordinator printer,
    ILocalizationService localization,
    TimeProvider timeProvider) : IAsyncDisposable
{
    private bool _disposed;
    private IssueEditingSession? _session;

    public event Action? StateChanged;

    public StoreSettings? Settings => _session?.Settings;
    public IReadOnlyList<Category> Categories => _session?.Categories ?? [];
    public IReadOnlyList<Product> Products => _session?.Products ?? [];
    public IssueWarrantyForm Form => Session.Form;
    public EditContext? EditContext => _session?.EditContext;
    public IssueWorkflowLoadState LoadState { get; private set; } = IssueWorkflowLoadState.Loading;
    public bool Loading => LoadState == IssueWorkflowLoadState.Loading;
    public bool Initialized => LoadState == IssueWorkflowLoadState.Editing;
    public bool DraftFailed => drafts.Failed;
    public bool IssueFailed { get; private set; }
    public bool Issuing { get; private set; }
    public bool ClearingDraft { get; private set; }
    public bool Busy => Issuing || ClearingDraft;
    public bool InkSaver { get; private set; }
    public string? LoadError { get; private set; }
    public bool ProductSuggestionsUnavailable { get; private set; }
    public string DraftStatus => drafts.Status;
    public string? IssueStatus { get; private set; }
    public WarrantyIssueResult? IssuedResult { get; private set; }
    public WarrantyRecord? IssuedWarranty => IssuedResult?.Warranty;
    public WarrantySnapshot? IssuedSnapshot => IssuedResult?.Snapshot;

    private IssueEditingSession Session => _session
        ?? throw new InvalidOperationException("The issue editing session has not been loaded.");

    public WarrantyTemplate CurrentTemplate => templates.Get(Form.TemplateKey);

    public IReadOnlyList<Product> AvailableProducts => Products
        .Where(product =>
            Session.Catalog.FindActive(product.CategoryKey) is not null
            && Settings!.OfferedDurationsMonths.Contains(product.DurationMonths))
        .ToList();

    public DateOnly Today => DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

    private Category CurrentCategory => Session.Catalog.SelectOrFirst(Form.CategoryKey);

    public WarrantyDisplayModel PreviewModel =>
        IssuedWarranty is not null && IssuedSnapshot is not null
            ? displayModels.Build(
                IssuedWarranty,
                IssuedSnapshot,
                CurrentTemplate.Illustration)
            : displayModels.Build(
                Settings!,
                CurrentCategory,
                ToDraft(),
                Categories,
                illustration: CurrentTemplate.Illustration);

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        drafts.StateChanged += OnDraftStateChanged;
        var result = await workspaceService.LoadAsync(cancellationToken);
        if (result is OperationResult<IssueWorkspaceData>.Failed failed)
        {
            LoadError = failed.Error;
            LoadState = IssueWorkflowLoadState.LoadFailed;
            NotifyStateChanged();
            return;
        }

        var workspace = ((OperationResult<IssueWorkspaceData>.Succeeded)result).Value;
        ProductSuggestionsUnavailable = workspace.ProductWarning is not null;
        var form = workspace.Draft is null
            ? CreateDefaultForm(workspace.Settings, workspace.Categories)
            : IssueWarrantyForm.FromDraft(workspace.Draft, Today);
        _session = new IssueEditingSession(
            workspace.Settings,
            workspace.Categories,
            workspace.Products,
            form);
        NormalizeFormSelections();
        InkSaver = workspace.Settings.InkSaver;
        drafts.Initialize(workspace.Draft, workspace.DraftWarning);

        LoadState = IssueWorkflowLoadState.Editing;
        await RecoverIssueAsync(cancellationToken);
        NotifyStateChanged();
    }

    public void SetInkSaver(bool value) => InkSaver = value;

    public Task<OperationResult> PrepareToLeaveAsync() => Busy
        ? Task.FromResult(OperationResult.Failure(new OperationError(
            "Issue.OperationInProgress", localization["Issue_WaitForOperation"])))
        : drafts.FlushAsync();

    public void NotifyChanged(string propertyName)
    {
        ResetIssuedState();
        EditContext!.NotifyFieldChanged(new FieldIdentifier(Form, propertyName));
        drafts.Schedule(CreateDraft());
    }

    public void ApplyProduct(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (!AvailableProducts.Any(item => item.Id == product.Id)) return;

        ResetIssuedState();
        Form.BrandModel = product.BrandModel;
        Form.CategoryKey = product.CategoryKey;
        Form.Price = product.PriceCents / 100m;
        Form.DurationMonths = product.DurationMonths;
        foreach (var propertyName in new[]
                 {
                     nameof(Form.BrandModel),
                     nameof(Form.CategoryKey),
                     nameof(Form.Price),
                     nameof(Form.DurationMonths)
                 })
        {
            EditContext!.NotifyFieldChanged(new FieldIdentifier(Form, propertyName));
        }

        drafts.Schedule(CreateDraft());
        NotifyStateChanged();
    }

    public async Task ClearDraftAsync()
    {
        if (Busy) return;

        if (!issueRecovery.IsReady)
        {
            await RecoverIssueAsync();
            if (!issueRecovery.IsReady)
            {
                NotifyStateChanged();
                return;
            }

            if (IssuedWarranty is not null)
            {
                NotifyStateChanged();
                return;
            }
        }

        if (issueRecovery.HasPendingRequest)
        {
            var recovery = await issueRecovery.RecoverPendingAsync();
            if (recovery is OperationResult<WarrantyIssueResult?>.Failed recoveryFailure)
            {
                IssueFailed = true;
                IssueStatus = recoveryFailure.Error;
                NotifyStateChanged();
                return;
            }

            var recovered = ((OperationResult<WarrantyIssueResult?>.Succeeded)recovery).Value;
            if (recovered is not null)
            {
                IssuedResult = recovered;
                RestoreIssuedForm();
                drafts.CompleteIssue();
                IssueStatus = localization.Format("Issue_Issued", recovered.Warranty.Number);
                IssueFailed = false;
                await issueRecovery.CompleteAsync();
                NotifyStateChanged();
                return;
            }

            await issueRecovery.CompleteAsync();
        }

        ClearingDraft = true;
        NotifyStateChanged();
        try
        {
            var result = await drafts.ClearAsync();
            if (result is OperationResult.Failed) return;

            Session.ReplaceForm(CreateDefaultForm(Settings!, Categories));
            IssuedResult = null;
            IssueStatus = null;
            await issueRecovery.CompleteAsync();
            IssueFailed = false;
        }
        finally
        {
            ClearingDraft = false;
            NotifyStateChanged();
        }
    }

    public async Task IssueAndPrintAsync()
    {
        if (Busy) return;

        if (!issueRecovery.IsReady)
        {
            await RecoverIssueAsync();
            if (!issueRecovery.IsReady)
            {
                NotifyStateChanged();
                return;
            }
        }

        if (IssuedWarranty is not null)
        {
            Issuing = true;
            try
            {
                await PrintIssuedAsync();
            }
            finally
            {
                Issuing = false;
                NotifyStateChanged();
            }
            return;
        }

        Issuing = true;
        try
        {
            await drafts.WaitForIdleAsync();
            IssueFailed = false;
            IssueStatus = localization["Issue_Saving"];
            NotifyStateChanged();

            var prepared = await issueRecovery.PrepareAsync(timeProvider.GetLocalNow());
            if (prepared is OperationResult<WarrantyIssueRequest>.Failed prepareFailure)
            {
                IssueFailed = true;
                IssueStatus = prepareFailure.Error;
                return;
            }
            var request = ((OperationResult<WarrantyIssueRequest>.Succeeded)prepared).Value;
            var result = await issuer.IssueAsync(ToDraft(), request);
            if (result is OperationResult<WarrantyIssueResult>.Failed failed)
            {
                var recovered = await issueRecovery.RecoverPendingAsync();
                if (recovered is OperationResult<WarrantyIssueResult?>.Succeeded { Value: not null } recovery)
                {
                    result = OperationResult<WarrantyIssueResult>.Success(recovery.Value);
                }
                else
                {
                    IssueFailed = true;
                    IssueStatus = recovered is OperationResult<WarrantyIssueResult?>.Failed recoverIssueFailure
                        ? recoverIssueFailure.Error
                        : failed.Error;
                    return;
                }
            }

            var issued = ((OperationResult<WarrantyIssueResult>.Succeeded)result).Value;
            IssuedResult = issued;
            drafts.CompleteIssue();
            IssueStatus = localization.Format("Issue_Issued", issued.Warranty.Number);
            await issueRecovery.CompleteAsync();
            var rendered = await rendering.WaitForRenderAsync(NotifyStateChanged);
            if (rendered is OperationResult.Failed renderFailure)
                ApplyPrintResult(renderFailure);
            else if (!_disposed)
                await PrintIssuedAsync();
        }
        finally
        {
            Issuing = false;
            NotifyStateChanged();
        }
    }

    public string TemplateName(WarrantyTemplate template) =>
        localization[template.Design.NameResourceKey];

    private IssueWarrantyForm CreateDefaultForm(
        StoreSettings settings,
        IReadOnlyList<Category> categories)
    {
        var category = new CategorySelection(categories).SelectOrFirst(key: null);
        return new IssueWarrantyForm
        {
            CategoryKey = category.Key,
            PurchaseDate = Today,
            ValidFrom = Today,
            DurationMonths = PreferredDuration(category, settings),
            TemplateKey = settings.DefaultTemplate,
            Today = Today
        };
    }

    private void NormalizeFormSelections()
    {
        if (Session.Catalog.FindActive(Form.CategoryKey) is null)
            Form.CategoryKey = Session.Catalog.SelectOrFirst(Form.CategoryKey).Key;
        if (!Settings!.OfferedDurationsMonths.Contains(Form.DurationMonths))
            Form.DurationMonths = PreferredDuration(CurrentCategory, Settings);
        Form.TemplateKey = templates.Get(Form.TemplateKey).Key;
    }

    private static int PreferredDuration(Category category, StoreSettings settings) =>
        category.DefaultDurationsMonths.FirstOrDefault(
            settings.OfferedDurationsMonths.Contains,
            settings.OfferedDurationsMonths.FirstOrDefault(12));

    private async Task PrintIssuedAsync()
    {
        NotifyStateChanged();
        var result = await printer.PrintAsync(CurrentTemplate, InkSaver);
        ApplyPrintResult(result);
    }

    private void ApplyPrintResult(OperationResult result)
    {
        if (result is not OperationResult.Failed failed) return;

        IssueFailed = true;
        IssueStatus = failed.Error;
        NotifyStateChanged();
    }

    private DraftWarranty CreateDraft() => Form.ToDraft(timeProvider.GetUtcNow());

    private DraftWarranty ToDraft() => drafts.Track(CreateDraft());

    private async Task RecoverIssueAsync(CancellationToken cancellationToken = default)
    {
        var recovery = await issueRecovery.InitializeAsync(cancellationToken);
        if (recovery is OperationResult<WarrantyIssueResult?>.Failed failed)
        {
            IssueFailed = true;
            IssueStatus = failed.Error;
            return;
        }

        var recovered = ((OperationResult<WarrantyIssueResult?>.Succeeded)recovery).Value;
        if (recovered is null)
        {
            IssueFailed = false;
            IssueStatus = null;
            return;
        }

        IssuedResult = recovered;
        RestoreIssuedForm();
        drafts.CompleteIssue();
        IssueFailed = false;
        IssueStatus = localization.Format("Issue_Issued", recovered.Warranty.Number);
        await issueRecovery.CompleteAsync(cancellationToken);
    }

    private void RestoreIssuedForm()
    {
        var warranty = IssuedResult?.Warranty
            ?? throw new InvalidOperationException("No issued warranty is available.");
        Session.ReplaceForm(IssueWarrantyForm.FromDraft(new DraftWarranty
        {
            ServiceType = warranty.ServiceType ?? WarrantyServiceType.Sale,
            RepairNote = warranty.RepairNote,
            CategoryKey = warranty.CategoryKey,
            BrandModel = warranty.BrandModel,
            Serial = warranty.Serial,
            PurchaseDate = warranty.PurchaseDate,
            ValidFrom = warranty.ValidFrom,
            PriceCents = warranty.PriceCents,
            DurationMonths = warranty.DurationMonths,
            TemplateKey = warranty.LastTemplateKey,
            SavedAt = warranty.IssuedAt
        }, Today));
    }

    private void ResetIssuedState()
    {
        IssuedResult = null;
        IssueFailed = false;
        IssueStatus = null;
    }

    private void OnDraftStateChanged() => NotifyStateChanged();

    private void NotifyStateChanged() => StateChanged?.Invoke();

    public void NotifyRendered() => rendering.NotifyRendered();

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _disposed = true;
        rendering.NotifyRendered();
        drafts.StateChanged -= OnDraftStateChanged;
        await drafts.DisposeAsync();
    }
}

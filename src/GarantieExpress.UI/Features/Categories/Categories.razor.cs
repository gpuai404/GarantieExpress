using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using GarantieExpress.Application.Localization;
using GarantieExpress.Application.Common;
using GarantieExpress.Application.Categories;
using GarantieExpress.Application.Settings;
using GarantieExpress.Application.Warranties;
using GarantieExpress.Domain;
using GarantieExpress.UI.Features.Categories.Models;
using GarantieExpress.UI.Features.Settings;
using GarantieExpress.UI.Localization;

namespace GarantieExpress.UI.Features.Categories;

public partial class Categories
{
    [Inject] private ILocalizationService Localization { get; set; } = null!;
    [Inject] private CategoryCatalogService CategoryCatalog { get; set; } = null!;
    [Inject] private CustomCategoryDraftFactory DraftFactory { get; set; } = null!;
    [Inject] private StoreSettingsService Settings { get; set; } = null!;
    [Inject] private SvgSanitizer SvgSanitizer { get; set; } = null!;
    [Inject] private WarrantyDurationFormatter DurationFormatter { get; set; } = null!;
    [Inject] private ThemePresentation ThemePresentation { get; set; } = null!;

    private readonly Dictionary<IllustrationVariant, string> _uploadErrors = [];
    private List<Category> _categories = [];
    private StoreSettings? _settings;
    private CustomCategoryDraft? _draft;
    private bool _loading = true;
    private bool _saving;
    private bool _failed;
    private bool _useThemeColor = true;
    private string? _message;

    private string KeyPreview => _draft?.ExistingKey
        ?? CategoryKeyGenerator.Generate(_draft?.Name ?? string.Empty, _categories.Select(category => category.Key));

    protected override async Task OnInitializedAsync()
    {
        var settingsTask = Settings.LoadAsync();
        var categoriesTask = CategoryCatalog.LoadAsync();
        await Task.WhenAll(settingsTask, categoriesTask);
        var settings = await settingsTask;
        var categories = await categoriesTask;
        if (settings is not OperationResult<StoreSettings>.Succeeded settingsSucceeded
            || categories is not OperationResult<IReadOnlyList<Category>>.Succeeded categoriesSucceeded)
        {
            SetMessage(
                settings is OperationResult<StoreSettings>.Failed failedSettings
                    ? failedSettings.Error
                    : categories is OperationResult<IReadOnlyList<Category>>.Failed failedCategories
                        ? failedCategories.Error
                        : Localization["Categories_LoadFailed"],
                true);
        }
        else
        {
            _settings = settingsSucceeded.Value;
            _categories = categoriesSucceeded.Value.ToList();
        }

        _loading = false;
    }

    private void StartNew()
    {
        _draft = new CustomCategoryDraft
        {
            DefaultDurationMonths = _settings!.OfferedDurationsMonths.FirstOrDefault(12)
        };
        ResetUploads();
        SetMessage(null, false);
    }

    private void StartEdit(Category category)
    {
        _draft = DraftFactory.CreateForEdit(category);
        ResetUploads();
        SetMessage(null, false);
    }

    private void StartDuplicate(Category category)
    {
        _draft = DraftFactory.CreateDuplicate(category);
        ResetUploads();
        SetMessage(Localization["Categories_DuplicateReady"], false);
    }

    private void CancelEdit()
    {
        _draft = null;
        ResetUploads();
        SetMessage(null, false);
    }

    private async Task UploadSvgAsync(IllustrationVariant variant, InputFileChangeEventArgs args)
    {
        var targetDraft = _draft;
        if (targetDraft is null) return;

        var useThemeColor = _useThemeColor;
        _uploadErrors.Remove(variant);
        var file = args.File;
        if (file.Size > SvgSanitizer.MaximumBytes)
        {
            _uploadErrors[variant] = Localization["Categories_SvgTooLarge"];
            return;
        }

        try
        {
            await using var stream = file.OpenReadStream(SvgSanitizer.MaximumBytes);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var source = await reader.ReadToEndAsync();
            if (!ReferenceEquals(_draft, targetDraft)) return;

            var result = SvgSanitizer.Sanitize(source, useThemeColor);
            if (result is OperationResult<string>.Failed failed)
            {
                _uploadErrors[variant] = failed.Error;
                return;
            }

            targetDraft.Svgs[variant] = ((OperationResult<string>.Succeeded)result).Value;
        }
        catch (IOException)
        {
            if (!ReferenceEquals(_draft, targetDraft)) return;
            _uploadErrors[variant] = Localization["Categories_SvgReadFailed"];
        }
    }

    private void RemoveSvg(IllustrationVariant variant)
    {
        _draft!.Svgs.Remove(variant);
        _uploadErrors.Remove(variant);
    }

    private string PreviewSvg(IllustrationVariant variant) => _draft is null
        ? string.Empty
        : CategoryDisplayResolver.ResolveIllustration(_draft.Svgs, variant);

    private async Task SaveAsync()
    {
        if (_saving) return;
        _saving = true;
        try
        {
            SetMessage(null, false);
            var result = await CategoryCatalog.SaveCustomAsync(_draft!.ToInput(), _categories);
            if (result is OperationResult<Category>.Failed failed)
            {
                SetMessage(failed.Error, true);
                return;
            }

            _draft = null;
            ResetUploads();
            await RefreshAfterChangeAsync(Localization["Categories_Saved"]);
        }
        finally { _saving = false; }
    }

    private async Task ToggleBarAsync((Category Category, ChangeEventArgs Args) request)
    {
        var (category, args) = request;
        var show = args.Value is bool value && value;
        var result = await CategoryCatalog.SetBarVisibilityAsync(category, show, _categories);
        var succeeded = result is OperationResult.Succeeded;
        if (succeeded) await RefreshAfterChangeAsync(Localization["Categories_BarUpdated"]);
        else SetMessage(((OperationResult.Failed)result).Error, true);
    }

    private async Task SetArchivedAsync((Category Category, bool Archived) request)
    {
        var (category, archived) = request;
        var result = await CategoryCatalog.SetArchivedAsync(category, archived);
        var succeeded = result is OperationResult.Succeeded;
        if (succeeded)
            await RefreshAfterChangeAsync(Localization[
                archived ? "Categories_ArchivedMessage" : "Categories_RestoredMessage"]);
        else SetMessage(((OperationResult.Failed)result).Error, true);
    }

    private async Task RefreshAfterChangeAsync(string successMessage)
    {
        var result = await CategoryCatalog.LoadAsync();
        if (result is OperationResult<IReadOnlyList<Category>>.Succeeded succeeded)
        {
            _categories = succeeded.Value.ToList();
            SetMessage(successMessage, false);
        }
        else SetMessage(Localization["Categories_SavedRefreshFailed"], true);
    }

    private void ResetUploads()
    {
        _uploadErrors.Clear();
    }

    private string VariantLabel(IllustrationVariant variant) => Localization[variant switch
    {
        IllustrationVariant.Detailed => "Categories_VariantDetailed",
        IllustrationVariant.Simple => "Categories_VariantSimple",
        _ => "Categories_VariantMinimal"
    }];

    private string TemplateVariantLabel(IllustrationVariant variant) => variant switch
    {
        IllustrationVariant.Detailed => "A4 · " + Localization["Categories_VariantDetailed"],
        IllustrationVariant.Simple => "A5 · " + Localization["Categories_VariantSimple"],
        _ => "A6 · " + Localization["Categories_VariantMinimal"]
    };

    private void SetMessage(string? message, bool failed)
    {
        _message = message;
        _failed = failed;
    }
}

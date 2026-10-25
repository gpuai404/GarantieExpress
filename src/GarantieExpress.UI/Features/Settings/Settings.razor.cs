using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using GarantieExpress.Application.Localization;
using GarantieExpress.Application.Common;
using GarantieExpress.Application.Settings;
using GarantieExpress.UI.Abstractions;
using GarantieExpress.UI.Features.Settings.Models;
using GarantieExpress.Domain;
using GarantieExpress.UI.Localization;

namespace GarantieExpress.UI.Features.Settings;

public partial class Settings
{
    [Inject] private StoreSettingsService SettingsService { get; set; } = null!;
    [Inject] private IImageService ImageService { get; set; } = null!;
    [Inject] private ILocalizationService Localization { get; set; } = null!;
    [Inject] private ThemePresentation ThemePresentation { get; set; } = null!;

    private SettingsFormModel? _settings;
    private bool _loading = true;
    private bool _saving;
    private bool _saveFailed;
    private string _durationText = string.Empty;
    private string _servicesText = string.Empty;
    private string _selectedPreset = $"built:{ThemePresets.DefaultKey}";
    private string _customPresetName = string.Empty;
    private string? _durationError;
    private string? _message;

    private IReadOnlyList<ContrastCheck> ContrastChecks => ThemePresentation.CheckContrast(_settings!.Theme);

    protected override async Task OnInitializedAsync()
    {
        var settingsResult = await SettingsService.LoadAsync();
        if (settingsResult is OperationResult<StoreSettings>.Succeeded settingsSucceeded)
        {
            _settings = SettingsFormModel.FromSettings(settingsSucceeded.Value);
            _durationText = string.Join(", ", _settings!.OfferedDurationsMonths);
            _servicesText = string.Join(Environment.NewLine, _settings.Services);
        }
        else
        {
            SetMessage(((OperationResult<StoreSettings>.Failed)settingsResult).Error, true);
        }

        _loading = false;
    }

    private async Task OnLogoChangedAsync(InputFileChangeEventArgs args)
    {
        SetMessage(Localization["Settings_LogoProcessing"], false);
        var result = await ImageService.DownscaleAsync(args.File);
        if (result is OperationResult<string>.Succeeded succeeded)
        {
            _settings!.LogoDataUri = succeeded.Value;
            SetMessage(Localization["Settings_LogoProcessed"], false);
        }
        else
        {
            SetMessage(((OperationResult<string>.Failed)result).Error, true);
        }
    }

    private void RemoveLogo()
    {
        _settings!.LogoDataUri = null;
        SetMessage(Localization["Settings_LogoRemoved"], false);
    }

    private void OnServicesChanged(ChangeEventArgs args)
    {
        _servicesText = args.Value?.ToString() ?? string.Empty;
        _settings!.Services = _servicesText
            .Split(['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    private void OnDurationsChanged(ChangeEventArgs args)
    {
        _durationText = args.Value?.ToString() ?? string.Empty;
        var parts = _durationText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is < 1 or > StoreSettings.MaximumDurationCount
            || parts.Any(part => !int.TryParse(part, out var value)
                || !WarrantyDuration.Create(value).IsSuccess))
        {
            _durationError = Localization["Settings_DurationError"];
            return;
        }

        _durationError = null;
        _settings!.OfferedDurationsMonths = parts.Select(int.Parse).ToList();
    }

    private void ApplyPreset(ChangeEventArgs args)
    {
        _selectedPreset = args.Value?.ToString() ?? $"built:{ThemePresets.DefaultKey}";
        ResetPreset();
    }

    private void OnCustomPresetNameChanged(ChangeEventArgs args)
    {
        _customPresetName = args.Value?.ToString() ?? string.Empty;
    }

    private void ResetPreset()
    {
        if (_selectedPreset.StartsWith("built:", StringComparison.Ordinal))
        {
            var key = _selectedPreset[6..];
            var preset = ThemePresets.All.FirstOrDefault(candidate => candidate.Key == key)
                ?? ThemePresets.All.First();
            _settings!.Theme = preset.Theme.Clone();
            return;
        }

        if (_selectedPreset.StartsWith("custom:", StringComparison.Ordinal)
            && int.TryParse(_selectedPreset[7..], out var index)
            && index >= 0
            && index < _settings!.SavedThemePresets.Count)
        {
            _settings.Theme = _settings.SavedThemePresets[index].Theme.Clone();
        }
    }

    private void SaveCustomPreset()
    {
        var name = _customPresetName.Trim();
        if (name.Length == 0)
        {
            SetMessage(Localization["Theme_PresetNameRequired"], true);
            return;
        }

        var existingIndex = _settings!.SavedThemePresets.FindIndex(preset =>
            string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase));
        var saved = new SavedThemePreset
        {
            Name = name[..Math.Min(name.Length, StoreSettings.MaximumPresetNameLength)],
            Theme = _settings.Theme.Clone()
        };
        if (existingIndex >= 0)
        {
            _settings.SavedThemePresets[existingIndex] = saved;
            _selectedPreset = $"custom:{existingIndex}";
        }
        else if (_settings.SavedThemePresets.Count < StoreSettings.MaximumPresetCount)
        {
            _settings.SavedThemePresets.Add(saved);
            _selectedPreset = $"custom:{_settings.SavedThemePresets.Count - 1}";
        }
        else
        {
            SetMessage(Localization["Theme_PresetLimit"], true);
            return;
        }

        _customPresetName = string.Empty;
        SetMessage(Localization["Theme_PresetSaved"], false);
    }

    private async Task SaveAsync()
    {
        if (_saving || _durationError is not null) return;
        _saving = true;
        try
        {
            _message = null;
            var result = await SettingsService.SaveAsync(_settings!.ToInput());
            if (result is OperationResult<StoreSettings>.Succeeded succeeded)
            {
                _settings = SettingsFormModel.FromSettings(succeeded.Value);
                _durationText = string.Join(", ", _settings.OfferedDurationsMonths);
                _servicesText = string.Join(Environment.NewLine, _settings.Services);
                SetMessage(Localization["Settings_Saved"], false);
            }
            else
            {
                SetMessage(((OperationResult<StoreSettings>.Failed)result).Error, true);
            }
        }
        finally { _saving = false; }
    }

    private void SetMessage(string? message, bool isError)
    {
        _message = message;
        _saveFailed = isError;
    }
}

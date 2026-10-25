namespace GarantieExpress.Domain;

public sealed class StoreSettings
{
    public const int MaximumNameLength = StoreProfile.MaximumNameLength;
    public const int MaximumSloganLength = StoreProfile.MaximumSloganLength;
    public const int MaximumAddressLength = StoreProfile.MaximumAddressLength;
    public const int MaximumContactLength = StoreProfile.MaximumContactLength;
    public const int MaximumServiceCount = StoreProfile.MaximumServiceCount;
    public const int MaximumServiceLength = StoreProfile.MaximumServiceLength;
    public const int MaximumLogoBytes = StoreProfile.MaximumLogoBytes;
    public const int MaximumFullTermsLength = WarrantyPolicy.MaximumFullTermsLength;
    public const int MaximumShortTermsLength = WarrantyPolicy.MaximumShortTermsLength;
    public const int MaximumDurationCount = WarrantyPolicy.MaximumDurationCount;
    public const int MaximumTemplateKeyLength = WarrantyPolicy.MaximumTemplateKeyLength;
    public const int MaximumPresetCount = 10;
    public const int MaximumPresetNameLength = SavedThemePalette.MaximumNameLength;

    private readonly IReadOnlyList<SavedThemePalette> _savedThemePresets;

    private StoreSettings(
        StoreProfile profile,
        ThemePalette theme,
        IReadOnlyList<SavedThemePalette> savedThemePresets,
        WarrantyPolicy warrantyPolicy,
        bool inkSaver,
        long version)
    {
        Profile = profile;
        Theme = theme;
        _savedThemePresets = Array.AsReadOnly(savedThemePresets.ToArray());
        WarrantyPolicy = warrantyPolicy;
        InkSaver = inkSaver;
        Version = version;
    }

    public StoreProfile Profile { get; }
    public WarrantyPolicy WarrantyPolicy { get; }
    public string Name => Profile.Name;
    public string Slogan => Profile.Slogan;
    public IReadOnlyList<string> Services => Profile.Services;
    public string? LogoDataUri => Profile.LogoDataUri;
    public string Address => Profile.Address;
    public string Phone => Profile.Phone;
    public string Vat => Profile.Vat;
    public ThemePalette Theme { get; }
    public IReadOnlyList<SavedThemePalette> SavedThemePresets => _savedThemePresets;
    public string DefaultTerms => WarrantyPolicy.DefaultTerms;
    public string ShortTerms => WarrantyPolicy.ShortTerms;
    public IReadOnlyList<WarrantyDuration> OfferedDurations => WarrantyPolicy.OfferedDurations;
    public IReadOnlyList<int> OfferedDurationsMonths => WarrantyPolicy.OfferedDurationsMonths;
    public string DefaultTemplate => WarrantyPolicy.DefaultTemplate;
    public bool InkSaver { get; }
    public WarrantyNumberPrefix NumberPrefix => WarrantyPolicy.NumberPrefix;
    public long Version { get; }

    public static DomainResult<StoreSettings, StoreSettingsError> Create(
        StoreProfile? profile,
        ThemePalette? theme,
        IReadOnlyList<SavedThemePalette>? savedThemePresets,
        WarrantyPolicy? warrantyPolicy,
        bool inkSaver,
        long version = 0)
    {
        if (version < 0) return Failure(StoreSettingsError.VersionInvalid);
        if (profile is null) return Failure(StoreSettingsError.NameRequired);
        if (theme is null) return Failure(StoreSettingsError.InvalidTheme);
        if (warrantyPolicy is null) return Failure(StoreSettingsError.InvalidTemplate);

        return CreateSavedPalettes(savedThemePresets).Match(
            presets => DomainResult<StoreSettings, StoreSettingsError>.Success(
                new StoreSettings(profile, theme, presets, warrantyPolicy, inkSaver, version)),
            Failure);
    }

    public DomainResult<StoreSettings, StoreSettingsError> WithVersion(long version) =>
        version < 0
            ? Failure(StoreSettingsError.VersionInvalid)
            : DomainResult<StoreSettings, StoreSettingsError>.Success(new StoreSettings(
                Profile,
                Theme,
                _savedThemePresets,
                WarrantyPolicy,
                InkSaver,
                version));

    private static DomainResult<IReadOnlyList<SavedThemePalette>, StoreSettingsError> CreateSavedPalettes(
        IReadOnlyList<SavedThemePalette>? presets)
    {
        if (presets is null)
            return DomainResult<IReadOnlyList<SavedThemePalette>, StoreSettingsError>.Success([]);
        if (presets.Count > MaximumPresetCount)
            return DomainResult<IReadOnlyList<SavedThemePalette>, StoreSettingsError>.Failure(
                StoreSettingsError.TooManySavedThemePresets);

        var saved = new List<SavedThemePalette>(presets.Count);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var preset in presets)
        {
            if (preset is null)
            {
                return DomainResult<IReadOnlyList<SavedThemePalette>, StoreSettingsError>.Failure(
                    StoreSettingsError.InvalidSavedThemePreset);
            }

            if (!names.Add(preset.Name))
            {
                return DomainResult<IReadOnlyList<SavedThemePalette>, StoreSettingsError>.Failure(
                    StoreSettingsError.InvalidSavedThemePreset);
            }

            saved.Add(preset);
        }

        return DomainResult<IReadOnlyList<SavedThemePalette>, StoreSettingsError>.Success(saved);
    }

    private static DomainResult<StoreSettings, StoreSettingsError> Failure(StoreSettingsError error) =>
        DomainResult<StoreSettings, StoreSettingsError>.Failure(error);
}

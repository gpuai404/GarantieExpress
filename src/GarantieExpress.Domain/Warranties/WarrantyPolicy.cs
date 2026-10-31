namespace GarantieExpress.Domain;

public sealed class WarrantyPolicy
{
    public const int MaximumFullTermsLength = 4_000;
    public const int MaximumShortTermsLength = 1_000;
    public const int MaximumDurationCount = 4;
    public const int MaximumTemplateKeyLength = 80;

    private WarrantyPolicy(
        string defaultTerms,
        string shortTerms,
        IReadOnlyList<WarrantyDuration> offeredDurations,
        string defaultTemplate,
        WarrantyNumberPrefix numberPrefix)
    {
        DefaultTerms = defaultTerms;
        ShortTerms = shortTerms;
        OfferedDurations = Array.AsReadOnly(offeredDurations.ToArray());
        OfferedDurationsMonths = Array.AsReadOnly(
            offeredDurations.Select(duration => duration.Months).ToArray());
        DefaultTemplate = defaultTemplate;
        NumberPrefix = numberPrefix;
    }

    public string DefaultTerms { get; }
    public string ShortTerms { get; }
    public IReadOnlyList<WarrantyDuration> OfferedDurations { get; }
    public IReadOnlyList<int> OfferedDurationsMonths { get; }
    public string DefaultTemplate { get; }
    public WarrantyNumberPrefix NumberPrefix { get; }

    public static DomainResult<WarrantyPolicy, StoreSettingsError> Create(
        string? defaultTerms,
        string? shortTerms,
        IReadOnlyList<int>? offeredDurationsMonths,
        string? defaultTemplate,
        string? numberPrefix)
    {
        var normalizedDefaultTerms = defaultTerms?.Trim() ?? string.Empty;
        if (normalizedDefaultTerms.Length == 0) return Failure(StoreSettingsError.TermsRequired);
        if (normalizedDefaultTerms.Length > MaximumFullTermsLength)
            return Failure(StoreSettingsError.TermsTooLong);

        var normalizedShortTerms = shortTerms?.Trim() ?? string.Empty;
        if (normalizedShortTerms.Length == 0) return Failure(StoreSettingsError.ShortTermsRequired);
        if (normalizedShortTerms.Length > MaximumShortTermsLength)
            return Failure(StoreSettingsError.ShortTermsTooLong);

        var durationValues = (offeredDurationsMonths ?? [])
            .Distinct()
            .Order()
            .ToList();
        if (durationValues.Count is < 1 or > MaximumDurationCount)
        {
            return Failure(StoreSettingsError.InvalidDurations);
        }

        var durations = new List<WarrantyDuration>(durationValues.Count);
        foreach (var months in durationValues)
        {
            var duration = WarrantyDuration.Create(months);
            if (duration is not DomainResult<WarrantyDuration, WarrantyIssuanceError>.Succeeded succeeded)
                return Failure(StoreSettingsError.InvalidDurations);
            durations.Add(succeeded.Value);
        }

        var normalizedTemplate = defaultTemplate?.Trim() ?? string.Empty;
        if (normalizedTemplate.Length is < 1 or > MaximumTemplateKeyLength)
            return Failure(StoreSettingsError.InvalidTemplate);

        return WarrantyNumberPrefix.Create(numberPrefix).Match(
            prefix => DomainResult<WarrantyPolicy, StoreSettingsError>.Success(new WarrantyPolicy(
                normalizedDefaultTerms,
                normalizedShortTerms,
                durations,
                normalizedTemplate,
                prefix)),
            _ => Failure(StoreSettingsError.InvalidNumberPrefix));
    }

    private static DomainResult<WarrantyPolicy, StoreSettingsError> Failure(StoreSettingsError error) =>
        DomainResult<WarrantyPolicy, StoreSettingsError>.Failure(error);
}

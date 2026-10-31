namespace GarantieExpress.Domain;

public sealed class StoreProfile
{
    public const int MaximumNameLength = 100;
    public const int MaximumSloganLength = 160;
    public const int MaximumAddressLength = 240;
    public const int MaximumContactLength = 40;
    public const int MaximumServiceCount = 8;
    public const int MaximumServiceLength = 120;
    public const int MaximumLogoBytes = 2 * 1024 * 1024;

    private static readonly string[] AllowedLogoPrefixes =
    [
        "data:image/png;base64,",
        "data:image/jpeg;base64,",
        "data:image/webp;base64,"
    ];

    private StoreProfile(
        string name,
        string slogan,
        IReadOnlyList<string> services,
        string? logoDataUri,
        string address,
        string phone,
        string vat)
    {
        Name = name;
        Slogan = slogan;
        Services = Array.AsReadOnly(services.ToArray());
        LogoDataUri = logoDataUri;
        Address = address;
        Phone = phone;
        Vat = vat;
    }

    public string Name { get; }
    public string Slogan { get; }
    public IReadOnlyList<string> Services { get; }
    public string? LogoDataUri { get; }
    public string Address { get; }
    public string Phone { get; }
    public string Vat { get; }

    public static DomainResult<StoreProfile, StoreSettingsError> Create(
        string? name,
        string? slogan,
        IReadOnlyList<string>? services,
        string? logoDataUri,
        string? address,
        string? phone,
        string? vat)
    {
        var normalizedName = name?.Trim() ?? string.Empty;
        if (normalizedName.Length == 0) return Failure(StoreSettingsError.NameRequired);
        if (normalizedName.Length > MaximumNameLength) return Failure(StoreSettingsError.NameTooLong);

        var normalizedSlogan = slogan?.Trim() ?? string.Empty;
        if (normalizedSlogan.Length > MaximumSloganLength)
            return Failure(StoreSettingsError.SloganTooLong);

        var normalizedAddress = address?.Trim() ?? string.Empty;
        if (normalizedAddress.Length > MaximumAddressLength)
            return Failure(StoreSettingsError.AddressTooLong);

        var normalizedPhone = phone?.Trim() ?? string.Empty;
        var normalizedVat = vat?.Trim() ?? string.Empty;
        if (normalizedPhone.Length > MaximumContactLength || normalizedVat.Length > MaximumContactLength)
            return Failure(StoreSettingsError.ContactValueTooLong);

        var normalizedServices = (services ?? [])
            .Select(service => service?.Trim() ?? string.Empty)
            .Where(service => service.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (normalizedServices.Count is < 1 or > MaximumServiceCount)
            return Failure(StoreSettingsError.InvalidServiceCount);
        if (normalizedServices.Any(service => service.Length > MaximumServiceLength))
            return Failure(StoreSettingsError.ServiceTooLong);

        if (!IsValidLogoDataUri(logoDataUri)) return Failure(StoreSettingsError.InvalidLogo);

        return DomainResult<StoreProfile, StoreSettingsError>.Success(new StoreProfile(
            normalizedName,
            normalizedSlogan,
            normalizedServices,
            logoDataUri,
            normalizedAddress,
            normalizedPhone,
            normalizedVat));
    }

    internal static bool IsValidLogoDataUri(string? value)
    {
        if (value is null) return true;

        var prefix = AllowedLogoPrefixes.FirstOrDefault(candidate =>
            value.StartsWith(candidate, StringComparison.OrdinalIgnoreCase));
        if (prefix is null) return false;

        var encoded = value.AsSpan(prefix.Length);
        if (encoded.IsEmpty || encoded.Length > ((MaximumLogoBytes + 2) / 3) * 4) return false;

        try
        {
            return Convert.FromBase64String(encoded.ToString()).Length is > 0 and <= MaximumLogoBytes;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static DomainResult<StoreProfile, StoreSettingsError> Failure(StoreSettingsError error) =>
        DomainResult<StoreProfile, StoreSettingsError>.Failure(error);
}

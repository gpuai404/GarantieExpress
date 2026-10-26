using System.Text.Json.Serialization;
using GarantieExpress.Application.Warranties.Issue;
using GarantieExpress.Domain;

namespace GarantieExpress.Infrastructure.Persistence.Payloads;

internal sealed class WarrantySnapshotPayload
{
    public int HashVersion { get; set; } = WarrantySnapshotHasher.CurrentVersion;
    public StoreDetailsPayload? Store { get; set; } = new();
    public ThemePayload? Theme { get; set; } = new();
    public string? LogoDataUri { get; set; }
    public string? FullTerms { get; set; } = string.Empty;
    public string? ShortTerms { get; set; } = string.Empty;
    public CategorySnapshotPayload? Category { get; set; } = new();
    public List<string>? CategoryBarItems { get; set; } = [];
    public List<DurationLabelPayload>? Durations { get; set; } = [];
    public WarrantyLabelsPayload? Labels { get; set; } = new(
        string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
        string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
        string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
        string.Empty, string.Empty);
    public string? CultureName { get; set; } = string.Empty;
}

internal static class WarrantySnapshotPayloadMapper
{
    public static DomainResult<WarrantySnapshot, WarrantySnapshotError> ToDomain(
        WarrantySnapshotPayload? document,
        string id)
    {
        if (document is null)
        {
            return DomainResult<WarrantySnapshot, WarrantySnapshotError>.Failure(
                WarrantySnapshotError.DataRequired);
        }

        var category = document.Category;
        if (category is null)
        {
            return CreateSnapshot(document, category: null, id);
        }

        return FieldSpecPayloadMapper.ToDomain(category.Fields).Match(
            fields => CreateSnapshot(
                document,
                new CategorySnapshot(
                    category.Key,
                    category.Name,
                    fields,
                    category.Svgs),
                id),
            _ => DomainResult<WarrantySnapshot, WarrantySnapshotError>.Failure(
                WarrantySnapshotError.CategoryFieldsInvalid));
    }

    public static WarrantySnapshotPayload FromDomain(WarrantySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new WarrantySnapshotPayload
        {
            HashVersion = WarrantySnapshotHasher.CurrentVersion,
            Store = StoreDetailsPayload.FromDomain(snapshot.Store),
            Theme = ThemePayloadMapper.FromDomain(snapshot.Theme),
            LogoDataUri = snapshot.LogoDataUri,
            FullTerms = snapshot.FullTerms,
            ShortTerms = snapshot.ShortTerms,
            Category = new CategorySnapshotPayload
            {
                Key = snapshot.Category.Key,
                Name = snapshot.Category.Name,
                Fields = snapshot.Category.Fields.Select(FieldSpecPayloadMapper.FromDomain).ToList(),
                Svgs = snapshot.Category.Svgs.ToDictionary(item => item.Key, item => item.Value)
            },
            CategoryBarItems = snapshot.CategoryBarItems.ToList(),
            Durations = snapshot.Durations
                .Select(DurationLabelPayload.FromDomain)
                .ToList(),
            Labels = FromLabels(snapshot.Labels),
            CultureName = snapshot.CultureName
        };
    }

    private static ThemePalette? ToTheme(ThemePayload? settings) =>
        ThemePayloadMapper.ToDomain(settings);

    private static DomainResult<WarrantySnapshot, WarrantySnapshotError> CreateSnapshot(
        WarrantySnapshotPayload document,
        CategorySnapshot? category,
        string id) =>
        WarrantySnapshot.CreateNew(
                document.Store?.ToDomain(),
                ToTheme(document.Theme),
                document.LogoDataUri,
                document.FullTerms,
                document.ShortTerms,
                category,
                document.CategoryBarItems,
                ToDurations(document.Durations),
                ToLabels(document.Labels),
                document.CultureName)
            .Bind(snapshot => snapshot.AssignId(id));

    private static IReadOnlyList<DurationLabelSnapshot>? ToDurations(
        IReadOnlyList<DurationLabelPayload>? durations) =>
        durations is null || durations.Any(item => item is null)
            ? null
            : durations.Select(item => item!.ToDomain()).ToList();

    private static WarrantyLabels? ToLabels(WarrantyLabelsPayload? labels) => labels is null
        ? null
        : new WarrantyLabels(
            labels.CertificateEyebrow,
            labels.CertificateTitle,
            labels.ProductDetails,
            labels.RegisteredProduct,
            labels.ProductType,
            labels.BrandModel,
            labels.Purchase,
            labels.PurchaseDate,
            labels.Price,
            labels.Warranty,
            labels.ValidUntil,
            labels.Terms,
            labels.Customer,
            labels.CustomerName,
            labels.Signature,
            labels.IssuedOn,
            labels.IdentifierHint,
            labels.ValidFrom,
            labels.ServiceType,
            labels.ServiceSale,
            labels.ServiceRepair,
            labels.RepairNote);

    private static WarrantyLabelsPayload FromLabels(WarrantyLabels labels) => new(
        labels.CertificateEyebrow,
        labels.CertificateTitle,
        labels.ProductDetails,
        labels.RegisteredProduct,
        labels.ProductType,
        labels.BrandModel,
        labels.Purchase,
        labels.PurchaseDate,
        labels.Price,
        labels.Warranty,
        labels.ValidUntil,
        labels.Terms,
        labels.Customer,
        labels.CustomerName,
        labels.Signature,
        labels.IssuedOn,
        labels.IdentifierHint,
        labels.ValidFrom,
        labels.ServiceType,
        labels.ServiceSale,
        labels.ServiceRepair,
        labels.RepairNote);
}

internal sealed class StoreDetailsPayload
{
    public string Name { get; set; } = string.Empty;
    public string Slogan { get; set; } = string.Empty;
    public List<string>? Services { get; set; } = [];
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Vat { get; set; } = string.Empty;

    internal StoreDetails ToDomain() =>
        new(Name, Slogan, Services ?? [], Address, Phone, Vat);

    internal static StoreDetailsPayload FromDomain(StoreDetails store) => new()
    {
        Name = store.Name,
        Slogan = store.Slogan,
        Services = store.Services.ToList(),
        Address = store.Address,
        Phone = store.Phone,
        Vat = store.Vat
    };
}

internal sealed class DurationLabelPayload
{
    public int Months { get; set; }
    public string Label { get; set; } = string.Empty;
    public string BadgeText { get; set; } = string.Empty;

    internal DurationLabelSnapshot ToDomain() => new(Months, Label, BadgeText);

    internal static DurationLabelPayload FromDomain(DurationLabelSnapshot duration) => new()
    {
        Months = duration.Months,
        Label = duration.Label,
        BadgeText = duration.BadgeText
    };
}

internal sealed record WarrantyLabelsPayload(
    string CertificateEyebrow,
    string CertificateTitle,
    string ProductDetails,
    string RegisteredProduct,
    string ProductType,
    string BrandModel,
    string Purchase,
    string PurchaseDate,
    string Price,
    string Warranty,
    string ValidUntil,
    string Terms,
    string Customer,
    string CustomerName,
    string Signature,
    string IssuedOn,
    string IdentifierHint,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ValidFrom = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ServiceType = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ServiceSale = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ServiceRepair = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RepairNote = null);

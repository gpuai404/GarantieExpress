using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GarantieExpress.Domain;

namespace GarantieExpress.Application.Warranties.Issue;

public static class WarrantySnapshotHasher
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Compute(WarrantySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var canonical = new
        {
            Store = new
            {
                snapshot.Store.Name,
                snapshot.Store.Slogan,
                snapshot.Store.Services,
                snapshot.Store.Address,
                snapshot.Store.Phone,
                snapshot.Store.Vat
            },
            Theme = new
            {
                snapshot.Theme.Primary,
                snapshot.Theme.Secondary,
                snapshot.Theme.Accent,
                snapshot.Theme.Surface,
                snapshot.Theme.Ink,
                snapshot.Theme.MutedInk,
                snapshot.Theme.Line,
                snapshot.Theme.Paper
            },
            snapshot.LogoDataUri,
            snapshot.FullTerms,
            snapshot.ShortTerms,
            Category = new
            {
                snapshot.Category.Key,
                snapshot.Category.Name,
                Fields = snapshot.Category.Fields.Select(field => new
                {
                    field.Kind,
                    field.Label,
                    field.Required,
                    field.LabelResourceKey
                }),
                Svgs = CanonicalIllustrations(snapshot.Category.Svgs)
            },
            snapshot.CategoryBarItems,
            Durations = snapshot.Durations.Select(duration => new
            {
                duration.Months,
                duration.Label,
                duration.BadgeText
            }),
            Labels = new
            {
                snapshot.Labels.CertificateEyebrow,
                snapshot.Labels.CertificateTitle,
                snapshot.Labels.ProductDetails,
                snapshot.Labels.RegisteredProduct,
                snapshot.Labels.ProductType,
                snapshot.Labels.BrandModel,
                snapshot.Labels.Purchase,
                snapshot.Labels.PurchaseDate,
                snapshot.Labels.Price,
                snapshot.Labels.Warranty,
                snapshot.Labels.ValidUntil,
                snapshot.Labels.Terms,
                snapshot.Labels.Customer,
                snapshot.Labels.CustomerName,
                snapshot.Labels.Signature,
                snapshot.Labels.IssuedOn,
                snapshot.Labels.IdentifierHint,
                snapshot.Labels.ValidFrom,
                snapshot.Labels.ServiceType,
                snapshot.Labels.ServiceSale,
                snapshot.Labels.ServiceRepair,
                snapshot.Labels.RepairNote
            },
            snapshot.CultureName
        };
        var json = JsonSerializer.Serialize(canonical, JsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    private static SortedDictionary<IllustrationVariant, string> CanonicalIllustrations(
        IReadOnlyDictionary<IllustrationVariant, string> illustrations)
    {
        var ordered = new SortedDictionary<IllustrationVariant, string>();
        foreach (var (variant, svg) in illustrations) ordered.Add(variant, svg);
        return ordered;
    }
}

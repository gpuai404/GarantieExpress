using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GarantieExpress.Domain;

namespace GarantieExpress.Application.Warranties.Issue;

public static class WarrantySnapshotHasher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Compute(WarrantySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var canonical = new
        {
            snapshot.Store,
            snapshot.Theme,
            snapshot.LogoDataUri,
            snapshot.FullTerms,
            snapshot.ShortTerms,
            Category = new
            {
                snapshot.Category.Key,
                snapshot.Category.Name,
                snapshot.Category.Fields,
                Svgs = CanonicalIllustrations(snapshot.Category.Svgs)
            },
            snapshot.CategoryBarItems,
            snapshot.Durations,
            snapshot.Labels,
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

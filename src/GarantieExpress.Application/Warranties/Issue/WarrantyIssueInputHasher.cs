using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GarantieExpress.Application.Warranties.Drafts;
using GarantieExpress.Domain;

namespace GarantieExpress.Application.Warranties.Issue;

internal static class WarrantyIssueInputHasher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Compute(
        int year,
        WarrantyNumberPrefix prefix,
        WarrantyIssuance issuance,
        DraftWarranty sourceDraft,
        WarrantySnapshot snapshot)
    {
        var canonical = new
        {
            Year = year,
            Prefix = prefix.Value,
            issuance.ServiceType,
            issuance.RepairNote,
            issuance.CategoryKey,
            issuance.BrandModel,
            issuance.Identifier,
            issuance.PurchaseDate,
            ValidFrom = issuance.Period.ValidFrom,
            ValidUntil = issuance.Period.ValidUntil,
            PriceCents = issuance.Price.Cents,
            DurationMonths = issuance.Duration.Months,
            issuance.TemplateKey,
            issuance.IssuedAt,
            DraftId = sourceDraft.Id,
            DraftVersion = sourceDraft.Version,
            SnapshotId = snapshot.Id
        };
        var json = JsonSerializer.Serialize(canonical, JsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }
}

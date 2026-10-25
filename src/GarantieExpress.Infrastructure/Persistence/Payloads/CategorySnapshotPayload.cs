using GarantieExpress.Domain;

namespace GarantieExpress.Infrastructure.Persistence.Payloads;

internal sealed class CategorySnapshotPayload
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<FieldSpecPayload> Fields { get; set; } = [];
    public Dictionary<IllustrationVariant, string> Svgs { get; set; } = [];
}

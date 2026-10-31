namespace GarantieExpress.Domain;

public sealed record CategorySnapshot(
    string Key,
    string Name,
    IReadOnlyList<FieldSpec> Fields,
    IReadOnlyDictionary<IllustrationVariant, string> Svgs);

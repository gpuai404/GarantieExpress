namespace GarantieExpress.Domain;

public sealed record FieldSpec
{
    public const int MaximumLabelLength = 120;
    public const int MaximumResourceKeyLength = 120;

    private FieldSpec(
        WarrantyFieldKind kind,
        string label,
        bool required,
        string? labelResourceKey)
    {
        Kind = kind;
        Label = label;
        Required = required;
        LabelResourceKey = labelResourceKey;
    }

    public WarrantyFieldKind Kind { get; }
    public string Label { get; }
    public bool Required { get; }
    public string? LabelResourceKey { get; }

    public static DomainResult<FieldSpec, FieldSpecError> Create(
        WarrantyFieldKind kind,
        string? label,
        bool required = true,
        string? labelResourceKey = null)
    {
        if (!Enum.IsDefined(kind)) return Failure(FieldSpecError.KindInvalid);

        var normalizedLabel = label?.Trim();
        if (normalizedLabel is not { Length: >= 1 and <= MaximumLabelLength })
            return Failure(FieldSpecError.LabelInvalid);

        var normalizedResourceKey = string.IsNullOrWhiteSpace(labelResourceKey)
            ? null
            : labelResourceKey.Trim();
        if (normalizedResourceKey is { Length: > MaximumResourceKeyLength })
            return Failure(FieldSpecError.LabelResourceKeyInvalid);

        return DomainResult<FieldSpec, FieldSpecError>.Success(new FieldSpec(
            kind,
            normalizedLabel,
            required,
            normalizedResourceKey));
    }

    public DomainResult<FieldSpec, FieldSpecError> WithResolvedLabel(string? label) =>
        Create(Kind, label, Required);

    private static DomainResult<FieldSpec, FieldSpecError> Failure(FieldSpecError error) =>
        DomainResult<FieldSpec, FieldSpecError>.Failure(error);
}

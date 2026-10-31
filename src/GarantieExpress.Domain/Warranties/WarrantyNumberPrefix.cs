namespace GarantieExpress.Domain;

public sealed record WarrantyNumberPrefix
{
    private WarrantyNumberPrefix(string value) => Value = value;

    public string Value { get; }

    public static DomainResult<WarrantyNumberPrefix, WarrantyIssuanceError> Create(
        string? prefix)
    {
        var value = prefix?.Trim().ToUpperInvariant();
        return value is { Length: >= 1 and <= 12 }
               && value.All(character =>
                   char.IsAsciiLetterOrDigit(character) || character == '-')
            ? DomainResult<WarrantyNumberPrefix, WarrantyIssuanceError>.Success(
                new WarrantyNumberPrefix(value))
            : DomainResult<WarrantyNumberPrefix, WarrantyIssuanceError>.Failure(
                WarrantyIssuanceError.NumberPrefixInvalid);
    }
}

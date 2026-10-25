namespace GarantieExpress.Domain;

public sealed record CategoryKey
{
    public const int MaximumLength = 80;

    private CategoryKey(string value) => Value = value;

    public string Value { get; }

    public static DomainResult<CategoryKey, CategoryKeyError> Create(string? value)
    {
        var normalized = value?.Trim();
        return normalized is { Length: >= 1 and <= MaximumLength }
            ? DomainResult<CategoryKey, CategoryKeyError>.Success(new CategoryKey(normalized))
            : DomainResult<CategoryKey, CategoryKeyError>.Failure(CategoryKeyError.Invalid);
    }

    public override string ToString() => Value;
}

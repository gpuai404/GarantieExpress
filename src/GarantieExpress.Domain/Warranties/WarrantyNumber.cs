namespace GarantieExpress.Domain;

public sealed record WarrantyNumber
{
    private WarrantyNumber(
        WarrantyNumberPrefix prefix,
        int year,
        int sequence)
    {
        Prefix = prefix;
        Year = year;
        Sequence = sequence;
        Value = FormattableString.Invariant($"{prefix.Value}-{year}-{sequence:D4}");
    }

    public WarrantyNumberPrefix Prefix { get; }
    public int Year { get; }
    public int Sequence { get; }
    public string Value { get; }

    public static DomainResult<WarrantyNumber, WarrantyRecordError> Create(
        WarrantyNumberPrefix prefix,
        int year,
        int sequence)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        if (year is < WarrantyRecord.MinimumYear or > WarrantyRecord.MaximumYear)
            return Failure(WarrantyRecordError.YearInvalid);
        if (sequence <= 0) return Failure(WarrantyRecordError.SequenceInvalid);

        return DomainResult<WarrantyNumber, WarrantyRecordError>.Success(
            new WarrantyNumber(prefix, year, sequence));
    }

    public static DomainResult<WarrantyNumber, WarrantyRecordError> Restore(
        string? value,
        int year,
        int sequence)
    {
        var normalized = value?.Trim();
        if (normalized is not { Length: >= 1 and <= WarrantyRecord.MaximumNumberLength })
            return Failure(WarrantyRecordError.NumberInvalid);
        if (year is < WarrantyRecord.MinimumYear or > WarrantyRecord.MaximumYear)
            return Failure(WarrantyRecordError.YearInvalid);
        if (sequence <= 0) return Failure(WarrantyRecordError.SequenceInvalid);

        var suffix = FormattableString.Invariant($"-{year}-{sequence:D4}");
        if (!normalized.EndsWith(suffix, StringComparison.Ordinal))
            return Failure(WarrantyRecordError.NumberInconsistent);

        var prefixValue = normalized[..^suffix.Length];
        return WarrantyNumberPrefix.Create(prefixValue).Match(
            prefix => Create(prefix, year, sequence),
            _ => Failure(WarrantyRecordError.NumberInconsistent));
    }

    private static DomainResult<WarrantyNumber, WarrantyRecordError> Failure(
        WarrantyRecordError error) =>
        DomainResult<WarrantyNumber, WarrantyRecordError>.Failure(error);
}

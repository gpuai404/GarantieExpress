namespace GarantieExpress.Domain;

public sealed class WarrantyRecord
{
    public const int MinimumYear = 2000;
    public const int MaximumYear = 9999;
    public const int MaximumNumberLength = 80;
    public const int MaximumCategoryKeyLength = global::GarantieExpress.Domain.CategoryKey.MaximumLength;
    public const int MaximumTemplateKeyLength = WarrantyPolicy.MaximumTemplateKeyLength;
    public const int MaximumSnapshotIdLength = 128;

    private readonly WarrantyRecordState _state;

    private WarrantyRecord(
        WarrantyNumber number,
        CategoryKey category,
        string brandModel,
        string identifier,
        DateOnly purchaseDate,
        WarrantyPrice price,
        WarrantyDuration duration,
        DateTimeOffset issuedAt,
        string lastTemplateKey,
        string snapshotId,
        WarrantyPeriod? period,
        WarrantyServiceType? serviceType,
        string? repairNote,
        WarrantyRecordState state)
    {
        IssuedNumber = number;
        CategoryIdentity = category;
        BrandModel = brandModel;
        Serial = identifier;
        PurchaseDate = purchaseDate;
        Price = price;
        Duration = duration;
        IssuedAt = issuedAt;
        LastTemplateKey = lastTemplateKey;
        SnapshotId = snapshotId;
        Period = period;
        ServiceType = serviceType;
        RepairNote = repairNote;
        _state = state;
    }

    public WarrantyNumber IssuedNumber { get; }
    public string Number => IssuedNumber.Value;
    public int Year => IssuedNumber.Year;
    public int Sequence => IssuedNumber.Sequence;
    public CategoryKey CategoryIdentity { get; }
    public string CategoryKey => CategoryIdentity.Value;
    public string BrandModel { get; }
    public string Serial { get; }
    public DateOnly PurchaseDate { get; }
    public WarrantyPrice Price { get; }
    public long PriceCents => Price.Cents;
    public WarrantyDuration Duration { get; }
    public int DurationMonths => Duration.Months;
    public DateTimeOffset IssuedAt { get; }
    public WarrantyStatus Status => _state.Status;
    public string LastTemplateKey { get; }
    public string SnapshotId { get; }
    public WarrantyPeriod? Period { get; }
    public DateOnly? ValidFrom => Period?.ValidFrom;
    public DateOnly? ValidUntil => Period?.ValidUntil;
    public WarrantyServiceType? ServiceType { get; }
    public string? RepairNote { get; }
    public long Version => _state.Version;

    public static DomainResult<WarrantyRecord, WarrantyRecordError> Restore(
        WarrantyNumber? number,
        CategoryKey? category,
        string? brandModel,
        string? identifier,
        DateOnly purchaseDate,
        WarrantyPrice? price,
        WarrantyDuration? duration,
        DateTimeOffset issuedAt,
        string? lastTemplateKey,
        string? snapshotId,
        WarrantyPeriod? period,
        WarrantyServiceType? serviceType,
        string? repairNote,
        WarrantyRecordState? state)
    {
        if (number is null) return Failure(WarrantyRecordError.NumberInvalid);
        if (category is null) return Failure(WarrantyRecordError.CategoryInvalid);
        if (price is null) return Failure(WarrantyRecordError.PriceInvalid);
        if (duration is null) return Failure(WarrantyRecordError.DurationInvalid);
        if (state is null) return Failure(WarrantyRecordError.DataRequired);

        var normalizedBrandModel = brandModel?.Trim();
        if (normalizedBrandModel is not { Length: >= 1 and <= WarrantyIssuance.MaximumBrandModelLength })
            return Failure(WarrantyRecordError.BrandModelInvalid);

        var normalizedIdentifier = identifier?.Trim();
        if (normalizedIdentifier is not { Length: >= 1 and <= WarrantyIssuance.MaximumIdentifierLength })
            return Failure(WarrantyRecordError.IdentifierInvalid);
        if (purchaseDate == default) return Failure(WarrantyRecordError.PurchaseDateInvalid);
        if (issuedAt == default || issuedAt.Year != number.Year)
            return Failure(WarrantyRecordError.IssuedAtInvalid);

        var normalizedTemplate = lastTemplateKey?.Trim();
        if (normalizedTemplate is not { Length: >= 1 and <= MaximumTemplateKeyLength })
            return Failure(WarrantyRecordError.TemplateInvalid);
        var normalizedSnapshot = snapshotId?.Trim();
        if (normalizedSnapshot is not { Length: >= 1 and <= MaximumSnapshotIdLength })
            return Failure(WarrantyRecordError.SnapshotInvalid);

        if (period is not null
            && (period.ValidFrom > DateOnly.MaxValue.AddMonths(-duration.Months)
                || period.ValidUntil != period.ValidFrom.AddMonths(duration.Months)))
        {
            return Failure(WarrantyRecordError.CoveragePeriodInvalid);
        }
        if (serviceType is { } type && !Enum.IsDefined(type))
            return Failure(WarrantyRecordError.ServiceTypeInvalid);

        var normalizedRepairNote = string.IsNullOrWhiteSpace(repairNote) ? null : repairNote.Trim();
        if (normalizedRepairNote is { Length: > WarrantyIssuance.MaximumRepairNoteLength }
            || normalizedRepairNote is not null && serviceType != WarrantyServiceType.Repair)
        {
            return Failure(WarrantyRecordError.RepairNoteInvalid);
        }

        return DomainResult<WarrantyRecord, WarrantyRecordError>.Success(new WarrantyRecord(
            number,
            category,
            normalizedBrandModel,
            normalizedIdentifier,
            purchaseDate,
            price,
            duration,
            issuedAt,
            normalizedTemplate,
            normalizedSnapshot,
            period,
            serviceType,
            normalizedRepairNote,
            state));
    }

    public static DomainResult<WarrantyRecord, WarrantyRecordError> CreateIssued(
        WarrantyIssuance issuance,
        WarrantyNumberPrefix prefix,
        int year,
        int sequence,
        string? snapshotId)
    {
        ArgumentNullException.ThrowIfNull(issuance);
        ArgumentNullException.ThrowIfNull(prefix);
        if (issuance.IssuedAt.Year != year)
            return Failure(WarrantyRecordError.IssuedAtInvalid);

        var normalizedSnapshot = snapshotId?.Trim();
        if (normalizedSnapshot is not { Length: >= 1 and <= MaximumSnapshotIdLength })
            return Failure(WarrantyRecordError.SnapshotInvalid);

        return WarrantyNumber.Create(prefix, year, sequence).Bind(number =>
            WarrantyRecordState.Create(WarrantyStatus.Active, version: 1).Map(state =>
                new WarrantyRecord(
                    number,
                    issuance.CategoryIdentity,
                    issuance.BrandModel,
                    issuance.Identifier,
                    issuance.PurchaseDate,
                    issuance.Price,
                    issuance.Duration,
                    issuance.IssuedAt,
                    issuance.TemplateKey,
                    normalizedSnapshot,
                    issuance.Period,
                    issuance.ServiceType,
                    issuance.RepairNote,
                    state)));
    }

    public WarrantyRecord Void() =>
        Status == WarrantyStatus.Voided ? this : Copy(_state.Void());

    public DomainResult<WarrantyRecord, WarrantyRecordError> WithVersion(long version) =>
        _state.WithVersion(version).Map(Copy);

    private WarrantyRecord Copy(WarrantyRecordState state) => new(
        IssuedNumber,
        CategoryIdentity,
        BrandModel,
        Serial,
        PurchaseDate,
        Price,
        Duration,
        IssuedAt,
        LastTemplateKey,
        SnapshotId,
        Period,
        ServiceType,
        RepairNote,
        state);

    private static DomainResult<WarrantyRecord, WarrantyRecordError> Failure(
        WarrantyRecordError error) =>
        DomainResult<WarrantyRecord, WarrantyRecordError>.Failure(error);
}

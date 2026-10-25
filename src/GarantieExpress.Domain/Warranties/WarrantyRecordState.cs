namespace GarantieExpress.Domain;

public sealed record WarrantyRecordState
{
    private WarrantyRecordState(WarrantyStatus status, long version)
    {
        Status = status;
        Version = version;
    }

    public WarrantyStatus Status { get; }
    public long Version { get; }

    public static DomainResult<WarrantyRecordState, WarrantyRecordError> Create(
        WarrantyStatus status,
        long version)
    {
        if (!Enum.IsDefined(status)) return Failure(WarrantyRecordError.StatusInvalid);
        if (version < 1) return Failure(WarrantyRecordError.VersionInvalid);

        return DomainResult<WarrantyRecordState, WarrantyRecordError>.Success(
            new WarrantyRecordState(status, version));
    }

    public WarrantyRecordState Void() =>
        Status == WarrantyStatus.Voided
            ? this
            : new WarrantyRecordState(WarrantyStatus.Voided, Version);

    public DomainResult<WarrantyRecordState, WarrantyRecordError> WithVersion(long version) =>
        Create(Status, version);

    private static DomainResult<WarrantyRecordState, WarrantyRecordError> Failure(
        WarrantyRecordError error) =>
        DomainResult<WarrantyRecordState, WarrantyRecordError>.Failure(error);
}

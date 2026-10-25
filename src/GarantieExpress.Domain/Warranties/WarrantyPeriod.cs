namespace GarantieExpress.Domain;

public sealed record WarrantyPeriod
{
    private WarrantyPeriod(DateOnly validFrom, DateOnly validUntil)
    {
        ValidFrom = validFrom;
        ValidUntil = validUntil;
    }

    public DateOnly ValidFrom { get; }
    public DateOnly ValidUntil { get; }

    public static DomainResult<WarrantyPeriod, WarrantyIssuanceError> Create(
        DateOnly? validFrom,
        WarrantyDuration duration)
    {
        ArgumentNullException.ThrowIfNull(duration);

        if (validFrom is null)
        {
            return DomainResult<WarrantyPeriod, WarrantyIssuanceError>.Failure(
                WarrantyIssuanceError.ValidFromRequired);
        }

        if (validFrom.Value == default
            || validFrom.Value > DateOnly.MaxValue.AddMonths(-duration.Months))
        {
            return DomainResult<WarrantyPeriod, WarrantyIssuanceError>.Failure(
                WarrantyIssuanceError.ValidFromOutOfRange);
        }

        return DomainResult<WarrantyPeriod, WarrantyIssuanceError>.Success(
            new WarrantyPeriod(
                validFrom.Value,
                validFrom.Value.AddMonths(duration.Months)));
    }
}

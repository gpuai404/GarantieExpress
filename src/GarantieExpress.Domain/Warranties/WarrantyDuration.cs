namespace GarantieExpress.Domain;

public sealed record WarrantyDuration
{
    public const int MaximumMonths = 120;

    private WarrantyDuration(int months) => Months = months;

    public int Months { get; }

    public static DomainResult<WarrantyDuration, WarrantyIssuanceError> Create(int months) =>
        months is >= 1 and <= MaximumMonths
            ? DomainResult<WarrantyDuration, WarrantyIssuanceError>.Success(
                new WarrantyDuration(months))
            : DomainResult<WarrantyDuration, WarrantyIssuanceError>.Failure(
                WarrantyIssuanceError.DurationInvalid);
}

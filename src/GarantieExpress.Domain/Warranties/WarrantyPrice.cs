namespace GarantieExpress.Domain;

public sealed record WarrantyPrice
{
    public const long MaximumCents = 9_999_999_900;

    private WarrantyPrice(long cents) => Cents = cents;

    public long Cents { get; }

    public static DomainResult<WarrantyPrice, WarrantyIssuanceError> Create(long cents) =>
        cents is > 0 and <= MaximumCents
            ? DomainResult<WarrantyPrice, WarrantyIssuanceError>.Success(
                new WarrantyPrice(cents))
            : DomainResult<WarrantyPrice, WarrantyIssuanceError>.Failure(
                WarrantyIssuanceError.PriceInvalid);

    public static DomainResult<WarrantyPrice, WarrantyIssuanceError> FromDecimal(decimal price)
    {
        if (price <= 0 || price > MaximumCents / 100m)
            return DomainResult<WarrantyPrice, WarrantyIssuanceError>.Failure(
                WarrantyIssuanceError.PriceInvalid);

        var rounded = decimal.Round(price * 100m, 0, MidpointRounding.AwayFromZero);
        return rounded <= MaximumCents
            ? Create(decimal.ToInt64(rounded))
            : DomainResult<WarrantyPrice, WarrantyIssuanceError>.Failure(
                WarrantyIssuanceError.PriceInvalid);
    }
}

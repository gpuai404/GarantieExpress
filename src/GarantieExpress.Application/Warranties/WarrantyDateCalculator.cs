using GarantieExpress.Domain;

namespace GarantieExpress.Application.Warranties;

public static class WarrantyDateCalculator
{
    public static DateOnly? CalculateEndDate(DateOnly? validFrom, int durationMonths) =>
        WarrantyDuration.Create(durationMonths)
            .Bind(duration => WarrantyPeriod.Create(validFrom, duration))
            .Match<DateOnly?>(period => period.ValidUntil, _ => null);

    public static bool CanCalculateEndDate(DateOnly? validFrom, int durationMonths) =>
        WarrantyDuration.Create(durationMonths)
            .Bind(duration => WarrantyPeriod.Create(validFrom, duration))
            .IsSuccess;
}

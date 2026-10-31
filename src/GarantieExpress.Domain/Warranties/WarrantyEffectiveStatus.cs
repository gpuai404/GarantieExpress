namespace GarantieExpress.Domain;

public enum WarrantyEffectiveStatus
{
    Active,
    Expired,
    Voided
}

public static class WarrantyStatusResolver
{
    public static WarrantyEffectiveStatus Resolve(
        WarrantyStatus status,
        DateOnly? validUntil,
        DateOnly currentDate)
    {
        if (status == WarrantyStatus.Voided) return WarrantyEffectiveStatus.Voided;
        return validUntil is { } endDate && endDate < currentDate
            ? WarrantyEffectiveStatus.Expired
            : WarrantyEffectiveStatus.Active;
    }
}

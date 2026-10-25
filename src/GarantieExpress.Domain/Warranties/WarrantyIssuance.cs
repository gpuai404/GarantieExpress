namespace GarantieExpress.Domain;

public sealed record WarrantyIssuance
{
    public const int MaximumBrandModelLength = 180;
    public const int MaximumIdentifierLength = 100;
    public const int MaximumRepairNoteLength = 300;
    public const int MaximumTemplateKeyLength = WarrantyPolicy.MaximumTemplateKeyLength;

    private WarrantyIssuance(
        WarrantyServiceType serviceType,
        string? repairNote,
        CategoryKey category,
        string brandModel,
        string identifier,
        DateOnly purchaseDate,
        WarrantyPeriod period,
        WarrantyPrice price,
        WarrantyDuration duration,
        string templateKey,
        DateTimeOffset issuedAt)
    {
        ServiceType = serviceType;
        RepairNote = repairNote;
        CategoryIdentity = category;
        BrandModel = brandModel;
        Identifier = identifier;
        PurchaseDate = purchaseDate;
        Period = period;
        Price = price;
        Duration = duration;
        TemplateKey = templateKey;
        IssuedAt = issuedAt;
    }

    public WarrantyServiceType ServiceType { get; }
    public string? RepairNote { get; }
    public CategoryKey CategoryIdentity { get; }
    public string CategoryKey => CategoryIdentity.Value;
    public string BrandModel { get; }
    public string Identifier { get; }
    public DateOnly PurchaseDate { get; }
    public WarrantyPeriod Period { get; }
    public WarrantyPrice Price { get; }
    public WarrantyDuration Duration { get; }
    public string TemplateKey { get; }
    public DateTimeOffset IssuedAt { get; }

    public static DomainResult<WarrantyIssuance, WarrantyIssuanceError> Create(
        WarrantyServiceType serviceType,
        string? repairNote,
        string? categoryKey,
        string? brandModel,
        string? identifier,
        DateOnly purchaseDate,
        DateOnly? validFrom,
        long priceCents,
        int durationMonths,
        string templateKey,
        DateTimeOffset issuedAt,
        DateOnly today)
    {
        if (!Enum.IsDefined(serviceType)) return Failure(WarrantyIssuanceError.ServiceTypeRequired);

        var normalizedRepairNote = string.IsNullOrWhiteSpace(repairNote) ? null : repairNote.Trim();
        if (normalizedRepairNote?.Length > MaximumRepairNoteLength)
            return Failure(WarrantyIssuanceError.RepairNoteTooLong);
        if (serviceType != WarrantyServiceType.Repair) normalizedRepairNote = null;

        var categoryResult = global::GarantieExpress.Domain.CategoryKey.Create(categoryKey);
        if (categoryResult is not DomainResult<CategoryKey, CategoryKeyError>.Succeeded categorySucceeded)
            return Failure(WarrantyIssuanceError.CategoryRequired);

        var normalizedBrandModel = brandModel?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedBrandModel))
            return Failure(WarrantyIssuanceError.BrandModelRequired);
        if (normalizedBrandModel.Length > MaximumBrandModelLength)
            return Failure(WarrantyIssuanceError.BrandModelTooLong);

        var normalizedIdentifier = identifier?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedIdentifier))
            return Failure(WarrantyIssuanceError.IdentifierRequired);
        if (normalizedIdentifier.Length > MaximumIdentifierLength)
            return Failure(WarrantyIssuanceError.IdentifierTooLong);

        if (purchaseDate == default || purchaseDate > today)
            return Failure(WarrantyIssuanceError.PurchaseDateInvalid);

        var normalizedTemplateKey = templateKey?.Trim();
        if (normalizedTemplateKey is not { Length: >= 1 and <= MaximumTemplateKeyLength })
            return Failure(WarrantyIssuanceError.TemplateInvalid);
        if (issuedAt == default) return Failure(WarrantyIssuanceError.IssuedAtInvalid);

        return WarrantyPrice.Create(priceCents).Bind(price =>
            WarrantyDuration.Create(durationMonths).Bind(duration =>
                WarrantyPeriod.Create(validFrom, duration).Map(period =>
                    new WarrantyIssuance(
                        serviceType,
                        normalizedRepairNote,
                        categorySucceeded.Value,
                        normalizedBrandModel,
                        normalizedIdentifier,
                        purchaseDate,
                        period,
                        price,
                        duration,
                        normalizedTemplateKey!,
                        issuedAt))));
    }

    private static DomainResult<WarrantyIssuance, WarrantyIssuanceError> Failure(
        WarrantyIssuanceError error) =>
        DomainResult<WarrantyIssuance, WarrantyIssuanceError>.Failure(error);
}

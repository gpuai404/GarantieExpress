namespace GarantieExpress.Domain;

public enum WarrantyIssuanceError
{
    ServiceTypeRequired,
    RepairNoteTooLong,
    CategoryRequired,
    BrandModelRequired,
    BrandModelTooLong,
    IdentifierRequired,
    IdentifierTooLong,
    PurchaseDateInvalid,
    ValidFromRequired,
    ValidFromOutOfRange,
    PriceInvalid,
    DurationInvalid,
    NumberPrefixInvalid,
    TemplateInvalid,
    IssuedAtInvalid
}

using GarantieExpress.Domain;
using GarantieExpress.Application.Localization;

namespace GarantieExpress.Application.Warranties.Issue;

public static class WarrantyLabelFactory
{
    public static WarrantyLabels Create(ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(localization);
        return new(
            localization["Template_CertificateEyebrow"],
            localization["Template_CertificateTitle"],
            localization["Template_ProductDetails"],
            localization["Template_RegisteredProduct"],
            localization["Template_ProductType"],
            localization["Template_BrandModel"],
            localization["Template_Purchase"],
            localization["Template_PurchaseDate"],
            localization["Template_Price"],
            localization["Settings_Warranty"],
            localization["Template_ValidUntil"],
            localization["Template_Terms"],
            localization["Template_Customer"],
            localization["Template_CustomerName"],
            localization["Template_Signature"],
            localization["Template_IssuedOn"],
            localization["Template_IdentifierHint"],
            localization["Template_ValidFrom"],
            localization["Template_ServiceType"],
            localization["Template_ServiceSale"],
            localization["Template_ServiceRepair"],
            localization["Template_RepairNote"]);
    }
}

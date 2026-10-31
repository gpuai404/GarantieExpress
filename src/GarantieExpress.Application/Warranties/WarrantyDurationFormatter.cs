using GarantieExpress.Application.Localization;
using GarantieExpress.Domain;

namespace GarantieExpress.Application.Warranties;

public sealed class WarrantyDurationFormatter(ILocalizationService localization)
{
    public string BuildBadgeText(int durationMonths)
    {
        if (!WarrantyDuration.Create(durationMonths).IsSuccess) return string.Empty;
        if (durationMonths % 12 != 0)
            return localization.Format("Warranty_BadgeMonths", durationMonths);

        var years = durationMonths / 12;
        return localization.Format(
            "Warranty_BadgeYears",
            years,
            localization[years == 1 ? "Warranty_YearOne" : "Warranty_Years"]);
    }

    public string FormatDuration(int durationMonths)
    {
        if (!WarrantyDuration.Create(durationMonths).IsSuccess) return string.Empty;
        return durationMonths % 12 == 0
            ? durationMonths == 12
                ? localization["Warranty_YearDurationOne"]
                : localization.Format("Warranty_YearDuration", durationMonths / 12)
            : durationMonths == 1
                ? localization["Warranty_MonthOne"]
                : localization.Format("Warranty_Months", durationMonths);
    }
}

using GarantieExpress.Domain;

namespace GarantieExpress.UI.Components.Warranty.Presentation;

public sealed record DurationOption(int Months, string Label, bool IsSelected);

public sealed record ServiceOption(string Label, bool IsSelected);

public sealed record WarrantyDisplayModel(
    string Number,
    string StoreName,
    string Slogan,
    IReadOnlyList<string> Services,
    string? LogoDataUri,
    string Address,
    string Phone,
    string Vat,
    string CategoryName,
    IReadOnlyList<string> CategoryBarItems,
    string IllustrationSvg,
    string BrandModel,
    string SerialLabel,
    string Serial,
    string PurchaseDate,
    string Price,
    IReadOnlyList<DurationOption> Durations,
    string EndDate,
    string BadgeText,
    string Terms,
    string ShortTerms,
    string IssuedDate,
    ThemePalette Theme,
    WarrantyLabels Labels,
    string? ValidFrom = null,
    IReadOnlyList<ServiceOption>? ServiceOptions = null,
    string? RepairNote = null);

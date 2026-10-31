using GarantieExpress.Domain;

namespace GarantieExpress.Application.Warranties;

public static class WarrantyRules
{
    public const int MaximumDurationMonths = WarrantyDuration.MaximumMonths;
    public const long MaximumPriceCents = WarrantyPrice.MaximumCents;
    public const int MaximumRepairNoteLength = WarrantyIssuance.MaximumRepairNoteLength;

    public static string? NormalizeRepairNote(string? note) =>
        string.IsNullOrWhiteSpace(note) ? null : note.Trim();

    public static bool IsValidDuration(int months) => WarrantyDuration.Create(months).IsSuccess;

    public static bool IsValidPurchaseDate(DateOnly date, DateOnly today) =>
        date != default && date <= today;

    public static bool IsValidPriceCents(long cents) => WarrantyPrice.Create(cents).IsSuccess;

    public static bool TryConvertPriceToCents(decimal price, out long cents)
    {
        var converted = WarrantyPrice.FromDecimal(price)
            .Match<long?>(value => value.Cents, _ => null);
        cents = converted ?? 0;
        return converted.HasValue;
    }

    public static bool IsValidNumberPrefix(string? prefix) =>
        WarrantyNumberPrefix.Create(prefix).IsSuccess;
}

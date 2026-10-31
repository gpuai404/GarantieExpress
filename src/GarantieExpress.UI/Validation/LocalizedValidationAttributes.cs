using System.ComponentModel.DataAnnotations;
using GarantieExpress.Application.Localization;
using GarantieExpress.Application.Warranties;
using GarantieExpress.UI.Localization;

namespace GarantieExpress.UI.Validation;

internal static class LocalizedValidation
{
    public static ValidationResult? Validate(
        bool isValid,
        string messageKey,
        ValidationContext validationContext)
    {
        if (isValid) return ValidationResult.Success;

        var errorMessage = validationContext.GetService(typeof(ILocalizationService))
            is ILocalizationService localization
            ? localization[messageKey]
            : Translations.Get(messageKey);

        return new ValidationResult(
            errorMessage,
            validationContext.MemberName is null ? null : [validationContext.MemberName]);
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class LocalizedRequiredAttribute(string messageKey) : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) =>
        LocalizedValidation.Validate(
            value is not null && (value is not string text || !string.IsNullOrWhiteSpace(text)),
            messageKey,
            validationContext);
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class LocalizedStringLengthAttribute(int maximumLength, string messageKey) : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) =>
        LocalizedValidation.Validate(
            value is not string text || text.Length <= maximumLength,
            messageKey,
            validationContext);
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class LocalizedNumberPrefixAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) =>
        LocalizedValidation.Validate(
            value is string text
            && text.Length is >= 1 and <= 12
            && text.All(character => char.IsAsciiLetterOrDigit(character) || character == '-'),
            "Validation_NumberPrefix",
            validationContext);
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class LocalizedPositivePriceAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) =>
        LocalizedValidation.Validate(
            value is decimal price && WarrantyRules.TryConvertPriceToCents(price, out _),
            "Validation_PricePositive",
            validationContext);
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class LocalizedDurationAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) =>
        LocalizedValidation.Validate(
            value is int months && WarrantyRules.IsValidDuration(months),
            "Validation_DurationRequired",
            validationContext);
}

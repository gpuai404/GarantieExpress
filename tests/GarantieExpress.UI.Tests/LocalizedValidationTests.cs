using System.ComponentModel.DataAnnotations;
using GarantieExpress.Application.Localization;
using GarantieExpress.UI.Features.Issue.Models;
using GarantieExpress.UI.Localization;
using Xunit;

namespace GarantieExpress.UI.Tests;

public sealed class LocalizedValidationTests
{
    [Fact]
    public async Task Validation_attributes_use_the_active_language()
    {
        var localization = await CreateLocalizationAsync("fr-BE");
        var form = new IssueWarrantyForm { BrandModel = string.Empty };
        var context = CreateContext(form, localization, nameof(form.BrandModel));

        var results = new List<ValidationResult>();
        Validator.TryValidateProperty(form.BrandModel, context, results);

        var result = Assert.Single(results);
        Assert.Equal("Saisissez la marque et le modèle.", result.ErrorMessage);
        Assert.Equal([nameof(form.BrandModel)], result.MemberNames);
    }

    [Fact]
    public async Task Model_validation_uses_the_active_language()
    {
        var localization = await CreateLocalizationAsync("en");
        var form = new IssueWarrantyForm
        {
            PurchaseDate = new DateOnly(2026, 10, 12),
            Today = new DateOnly(2026, 10, 11),
            ValidFrom = new DateOnly(2026, 10, 11),
            DurationMonths = 12
        };

        var results = form.Validate(CreateContext(form, localization)).ToList();

        var result = Assert.Single(results);
        Assert.Equal("Enter a purchase date that is not in the future.", result.ErrorMessage);
        Assert.Equal([nameof(form.PurchaseDate)], result.MemberNames);
    }

    private static async Task<LocalizationService> CreateLocalizationAsync(string language)
    {
        var localization = new LocalizationService();
        await localization.SetLanguageAsync(language);
        return localization;
    }

    private static ValidationContext CreateContext(
        object instance,
        ILocalizationService localization,
        string? memberName = null) => new(
            instance,
            new ValidationServiceProvider(localization),
            items: null)
        {
            MemberName = memberName
        };

    private sealed class ValidationServiceProvider(ILocalizationService localization) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(ILocalizationService) ? localization : null;
    }
}

using Microsoft.Extensions.DependencyInjection;
using GarantieExpress.Application.Localization;
using GarantieExpress.Application.Templates;
using GarantieExpress.UI.Abstractions;
using GarantieExpress.UI.Browser;
using GarantieExpress.UI.Components.Warranty.Presentation;
using GarantieExpress.UI.Components.Warranty;
using GarantieExpress.UI.Components.Warranty.Templates;
using GarantieExpress.UI.Features.Categories.Models;
using GarantieExpress.UI.Features.Issue.Workflow;
using GarantieExpress.UI.Features.Settings;
using GarantieExpress.UI.Localization;
using GarantieExpress.UI.Services;

namespace GarantieExpress.UI.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGarantieExpressUI(this IServiceCollection services)
    {
        services.AddScoped<LocalizationService>();
        services.AddScoped<ILocalizationService>(provider => provider.GetRequiredService<LocalizationService>());
        services.AddScoped<ILanguagePreferenceStore, BrowserLanguagePreferenceStore>();
        services.AddScoped<IPrintService, BrowserPrintService>();
        services.AddScoped<IImageService, BrowserImageService>();
        services.AddScoped<IIssuanceRecoveryStore, BrowserIssuanceRecoveryStore>();
        services.AddScoped<CustomCategoryDraftFactory>();
        services.AddScoped<WarrantyDisplayModelBuilder>();
        services.AddScoped<WarrantyPrintCoordinator>();
        services.AddScoped<PendingChangesCoordinator>();
        services.AddScoped<IssueWorkflowFactory>();
        services.AddSingleton<ThemePresentation>();
        services.AddSingleton<TemplateRegistry>();
        services.AddSingleton<ITemplateKeyResolver>(provider => provider.GetRequiredService<TemplateRegistry>());
        return services;
    }
}

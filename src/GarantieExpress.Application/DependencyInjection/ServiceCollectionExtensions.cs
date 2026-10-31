using Microsoft.Extensions.DependencyInjection;
using GarantieExpress.Application.Categories;
using GarantieExpress.Application.Initialization;
using GarantieExpress.Application.Products;
using GarantieExpress.Application.Settings;
using GarantieExpress.Application.Warranties;
using GarantieExpress.Application.Warranties.Drafts;
using GarantieExpress.Application.Warranties.History;
using GarantieExpress.Application.Warranties.Issue;

namespace GarantieExpress.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGarantieExpressApplication(this IServiceCollection services)
    {
        services.AddScoped<InitialApplicationDataFactory>();
        services.AddScoped<WarrantyIssueService>();
        services.AddScoped<WarrantyIssueRecoveryService>();
        services.AddScoped<WarrantyIssuancePolicy>();
        services.AddScoped<StoreSettingsService>();
        services.AddScoped<WarrantyDurationFormatter>();
        services.AddScoped<WarrantySnapshotFactory>();
        services.AddScoped<WarrantyDraftService>();
        services.AddScoped<IssueWorkspaceService>();
        services.AddScoped<WarrantyHistoryService>();
        services.AddScoped<CategoryCatalogService>();
        services.AddScoped<CategoryDisplayResolver>();
        services.AddScoped<ProductCatalogService>();
        services.AddScoped<SvgSanitizer>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}

using Microsoft.Extensions.DependencyInjection;
using GarantieExpress.Application.Categories;
using GarantieExpress.Application.Products;
using GarantieExpress.Application.Settings;
using GarantieExpress.Application.Warranties.Drafts;
using GarantieExpress.Application.Warranties.History;
using GarantieExpress.Application.Warranties.Issue;
using GarantieExpress.Infrastructure.Persistence.Sqlite;
using GarantieExpress.Infrastructure.Persistence.Sqlite.Initialization;
using GarantieExpress.Infrastructure.Persistence.Sqlite.Repositories;
using GarantieExpress.Infrastructure.Storage;

namespace GarantieExpress.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGarantieExpressInfrastructure(
        this IServiceCollection services,
        string? dataDirectory = null)
    {
        services.AddSingleton(_ => string.IsNullOrWhiteSpace(dataDirectory)
            ? new ApplicationPaths()
            : new ApplicationPaths(dataDirectory));
        services.AddSingleton<DatabaseOperationGate>();
        services.AddSingleton(typeof(SqliteOperationRunner<>));
        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<ISettingsRepository, SettingsRepository>();
        services.AddSingleton<ICategoryRepository, CategoryRepository>();
        services.AddSingleton<IProductRepository, ProductRepository>();
        services.AddSingleton<IDraftRepository, DraftRepository>();
        services.AddSingleton<IWarrantyRepository, WarrantyRepository>();
        services.AddSingleton<IWarrantyIssueRepository, WarrantyIssueRepository>();
        return services;
    }
}

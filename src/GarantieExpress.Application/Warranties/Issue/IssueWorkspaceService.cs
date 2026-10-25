using GarantieExpress.Application.Categories;
using GarantieExpress.Application.Common;
using GarantieExpress.Application.Localization;
using GarantieExpress.Application.Settings;
using GarantieExpress.Application.Products;
using GarantieExpress.Application.Warranties.Drafts;
using GarantieExpress.Domain;

namespace GarantieExpress.Application.Warranties.Issue;

public sealed class IssueWorkspaceService(
    StoreSettingsService settings,
    CategoryCatalogService categories,
    WarrantyDraftService drafts,
    IProductRepository products,
    ILocalizationService localization)
{
    public async Task<OperationResult<IssueWorkspaceData>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        var settingsTask = settings.LoadAsync(cancellationToken);
        var categoriesTask = categories.LoadAsync(
            includeArchived: false,
            cancellationToken);
        var draftTask = drafts.LoadAsync(cancellationToken);
        var productsTask = products.GetAllAsync(cancellationToken);
        await Task.WhenAll(settingsTask, categoriesTask, draftTask, productsTask);

        var settingsResult = await settingsTask;
        if (settingsResult is OperationResult<StoreSettings>.Failed failedSettings)
            return OperationResult<IssueWorkspaceData>.Failure(failedSettings.Details);

        var categoriesResult = await categoriesTask;
        if (categoriesResult is OperationResult<IReadOnlyList<Category>>.Failed failedCategories)
            return OperationResult<IssueWorkspaceData>.Failure(failedCategories.Details);

        var catalog = ((OperationResult<IReadOnlyList<Category>>.Succeeded)categoriesResult)
            .Value;
        if (catalog.Count == 0)
        {
            return OperationResult<IssueWorkspaceData>.Failure(new OperationError(
                "Warranty.NoCategories",
                localization["Issue_NoCategories"]));
        }

        var draftResult = await draftTask;
        var draft = draftResult.Match<DraftWarranty?>(value => value, _ => null);
        var draftWarning = draftResult.Match<OperationError?>(_ => null, error => error);
        var productsResult = await productsTask;
        var productCatalog = productsResult.Match<IReadOnlyList<Product>>(
            value => value,
            _ => []);
        var productWarning = productsResult.Match<OperationError?>(_ => null, error => error);
        return OperationResult<IssueWorkspaceData>.Success(new IssueWorkspaceData(
            ((OperationResult<StoreSettings>.Succeeded)settingsResult).Value,
            catalog,
            draft,
            draftWarning,
            productCatalog,
            productWarning));
    }
}

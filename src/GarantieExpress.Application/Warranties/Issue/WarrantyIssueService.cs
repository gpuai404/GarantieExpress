using GarantieExpress.Application.Categories;
using GarantieExpress.Application.Common;
using GarantieExpress.Application.Localization;
using GarantieExpress.Application.Settings;
using GarantieExpress.Application.Warranties.Drafts;
using GarantieExpress.Domain;

namespace GarantieExpress.Application.Warranties.Issue;

public sealed class WarrantyIssueService(
    StoreSettingsService settings,
    CategoryCatalogService categories,
    IWarrantyIssueRepository issues,
    WarrantySnapshotFactory snapshotFactory,
    WarrantyIssuancePolicy issuancePolicy,
    ILocalizationService localization)
{
    private sealed record PreparedWarrantyIssue(
        WarrantyIssueCommand Command,
        WarrantySnapshot Snapshot);

    public async Task<OperationResult<WarrantyIssueResult>> IssueAsync(
        DraftWarranty draft,
        WarrantyIssueRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(request);
        var settingsTask = settings.LoadAsync(cancellationToken);
        var categoriesTask = categories.LoadAsync(
            includeArchived: true,
            cancellationToken: cancellationToken);
        await Task.WhenAll(settingsTask, categoriesTask);

        var settingsResult = await settingsTask;
        var categoriesResult = await categoriesTask;
        return await settingsResult.Match(
            storeSettings => categoriesResult.Match(
                catalog => IssueWithContextAsync(
                    storeSettings,
                    catalog,
                    draft,
                    request,
                    cancellationToken),
                FailedAsync),
            FailedAsync);
    }

    private Task<OperationResult<WarrantyIssueResult>> IssueWithContextAsync(
        StoreSettings settings,
        IReadOnlyList<Category> categories,
        DraftWarranty draft,
        WarrantyIssueRequest request,
        CancellationToken cancellationToken)
    {
        var category = categories.FirstOrDefault(item => item.Key == draft.CategoryKey);
        if (category is null)
        {
            return Task.FromResult(OperationResult<WarrantyIssueResult>.Failure(
                new OperationError(
                    "Warranty.CategoryRequired",
                    localization["Validation_CategoryRequired"],
                    nameof(DraftWarranty.CategoryKey))));
        }

        var now = request.RequestedAt;
        var validation = issuancePolicy.Validate(
            settings,
            category,
            draft,
            categories,
            now,
            DateOnly.FromDateTime(now.DateTime));
        return validation.Match(
            validated => PrepareIssuance(settings, category, categories, validated, request, now).Match(
                prepared => CommitIssuanceAsync(prepared, cancellationToken),
                error => Task.FromResult(OperationResult<WarrantyIssueResult>.Failure(error))),
            error => Task.FromResult(
                OperationResult<WarrantyIssueResult>.Failure(error)));
    }

    private static Task<OperationResult<WarrantyIssueResult>> FailedAsync(
        OperationError error) =>
        Task.FromResult(OperationResult<WarrantyIssueResult>.Failure(error));

    private OperationResult<PreparedWarrantyIssue> PrepareIssuance(
        StoreSettings settings,
        Category category,
        IReadOnlyList<Category> categories,
        WarrantyIssuePlan plan,
        WarrantyIssueRequest request,
        DateTimeOffset now)
    {
        var year = now.Year;
        var snapshot = snapshotFactory.Create(settings, category, categories);
        return snapshot.Match(
            value => OperationResult<PreparedWarrantyIssue>.Success(new PreparedWarrantyIssue(
                new WarrantyIssueCommand(
                    request.Id,
                    WarrantyIssueInputHasher.Compute(
                        year,
                        plan.NumberPrefix,
                        plan.Issuance,
                        plan.SourceDraft,
                        value),
                    year,
                    settings.Version,
                    categories
                        .OrderBy(item => item.Key, StringComparer.Ordinal)
                        .Select(item => new CategoryVersionExpectation(item.Key, item.Version))
                        .ToList(),
                    plan.NumberPrefix,
                    plan.Issuance,
                    value,
                    plan.SourceDraft),
                value)),
            OperationResult<PreparedWarrantyIssue>.Failure);
    }

    private async Task<OperationResult<WarrantyIssueResult>> CommitIssuanceAsync(
        PreparedWarrantyIssue prepared,
        CancellationToken cancellationToken)
    {
        var result = await issues.IssueAsync(prepared.Command, cancellationToken);
        return result.Match(
            warranty => OperationResult<WarrantyIssueResult>.Success(
                new WarrantyIssueResult(warranty, prepared.Snapshot)),
            OperationResult<WarrantyIssueResult>.Failure);
    }
}

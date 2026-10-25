using GarantieExpress.Application.Common;
using GarantieExpress.Application.Localization;
using GarantieExpress.Application.Warranties.Drafts;
using GarantieExpress.Domain;

namespace GarantieExpress.Application.Warranties.History;

public sealed class WarrantyHistoryService(
    IWarrantyRepository warranties,
    WarrantyDraftService drafts,
    TimeProvider timeProvider,
    ILocalizationService localization)
{
    public Task<OperationResult<WarrantyHistoryPage>> SearchAsync(
        string? query = null,
        int pageIndex = 0,
        CancellationToken cancellationToken = default) =>
        warranties.SearchAsync(
            new WarrantyHistoryPageRequest(query, localization.CurrentLanguage, pageIndex),
            cancellationToken);

    public Task<OperationResult<WarrantySnapshot?>> GetSnapshotAsync(
        WarrantyRecord warranty,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(warranty);
        return warranties.GetSnapshotAsync(warranty.SnapshotId, cancellationToken);
    }

    public Task<OperationResult<WarrantyRecord>> VoidAsync(
        WarrantyRecord warranty,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(warranty);
        return warranties.SaveStatusAsync(warranty.Void(), cancellationToken);
    }

    public Task<OperationResult> DeleteAsync(
        WarrantyRecord warranty,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(warranty);
        return warranties.DeleteAsync(warranty.Number, warranty.Version, cancellationToken);
    }

    public Task<OperationResult<DraftWarranty?>> GetDraftAsync(
        CancellationToken cancellationToken = default) =>
        drafts.LoadAsync(cancellationToken);

    public async Task<OperationResult<DraftWarranty>> DuplicateAsDraftAsync(
        WarrantyRecord warranty,
        DraftWarranty? draftToReplace = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(warranty);

        var draft = new DraftWarranty
        {
            Version = draftToReplace?.Version ?? 0,
            ServiceType = warranty.ServiceType ?? WarrantyServiceType.Sale,
            RepairNote = warranty.RepairNote,
            CategoryKey = warranty.CategoryKey,
            BrandModel = warranty.BrandModel,
            Serial = warranty.Serial,
            PurchaseDate = warranty.PurchaseDate,
            ValidFrom = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime),
            PriceCents = warranty.PriceCents,
            DurationMonths = warranty.DurationMonths,
            TemplateKey = warranty.LastTemplateKey,
            SavedAt = timeProvider.GetUtcNow()
        };
        var result = await drafts.SaveAsync(draft, cancellationToken);
        return result is OperationResult<DraftWarranty>.Failed
        { Details.Code: "Storage.StaleDraft" or "Storage.Constraint" }
            ? OperationResult<DraftWarranty>.Failure(new OperationError(
                "Draft.ReplacementConflict",
                localization["History_DraftChanged"]))
            : result;
    }
}

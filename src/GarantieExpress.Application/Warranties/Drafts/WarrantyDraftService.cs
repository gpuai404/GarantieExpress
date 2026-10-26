using GarantieExpress.Application.Common;
using GarantieExpress.Application.Warranties;
using GarantieExpress.Domain;

namespace GarantieExpress.Application.Warranties.Drafts;

public sealed class WarrantyDraftService(
    IDraftRepository repository,
    TimeProvider timeProvider)
{
    public Task<OperationResult<DraftWarranty?>> LoadAsync(
        CancellationToken cancellationToken = default) =>
        repository.GetAsync(cancellationToken);

    public Task<OperationResult<DraftWarranty>> SaveAsync(
        DraftWarranty draft,
        CancellationToken cancellationToken = default) =>
        Validate(draft).Match(
            value => repository.SaveAsync(value, cancellationToken),
            error => Task.FromResult(OperationResult<DraftWarranty>.Failure(error)));

    public Task<OperationResult> ClearAsync(
        string expectedId,
        long expectedVersion,
        CancellationToken cancellationToken = default) =>
        !DraftWarranty.IsValidIdentity(expectedId, expectedVersion)
            ? Task.FromResult(OperationResult.Failure(new OperationError(
                "Draft.InvalidIdentity", "The draft identity is invalid.")))
            : repository.ClearAsync(expectedId, expectedVersion, cancellationToken);

    private OperationResult<DraftWarranty> Validate(DraftWarranty? draft)
    {
        if (draft is null) return Invalid("DataRequired");
        if (!draft.HasValidIdentity) return Invalid("IdentityInvalid");
        if (!Enum.IsDefined(draft.ServiceType)) return Invalid("ServiceTypeInvalid");
        if (draft.RepairNote is not null
            && (draft.ServiceType != WarrantyServiceType.Repair
                || draft.RepairNote.Length > WarrantyIssuance.MaximumRepairNoteLength))
        {
            return Invalid("RepairNoteInvalid");
        }

        if (string.IsNullOrWhiteSpace(draft.CategoryKey)
            || draft.CategoryKey.Length > Category.MaximumKeyLength)
        {
            return Invalid("CategoryInvalid");
        }

        if (draft.BrandModel is null
            || draft.BrandModel.Length > WarrantyIssuance.MaximumBrandModelLength)
        {
            return Invalid("BrandModelInvalid");
        }

        if (draft.Serial is null
            || draft.Serial.Length > WarrantyIssuance.MaximumIdentifierLength)
        {
            return Invalid("IdentifierInvalid");
        }

        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        if (draft.PurchaseDate != default && draft.PurchaseDate > today)
            return Invalid("PurchaseDateInvalid");
        if (draft.ValidFrom is { } validFrom && validFrom == default)
            return Invalid("ValidFromInvalid");
        if (draft.PriceCents is < 0 or > WarrantyPrice.MaximumCents)
            return Invalid("PriceInvalid");
        if (!WarrantyDuration.Create(draft.DurationMonths).IsSuccess
            || draft.ValidFrom is not null
                && !WarrantyDateCalculator.CanCalculateEndDate(
                    draft.ValidFrom,
                    draft.DurationMonths))
        {
            return Invalid("DurationInvalid");
        }

        if (string.IsNullOrWhiteSpace(draft.TemplateKey)
            || draft.TemplateKey.Length > WarrantyIssuance.MaximumTemplateKeyLength)
        {
            return Invalid("TemplateInvalid");
        }

        return OperationResult<DraftWarranty>.Success(draft);
    }

    private static OperationResult<DraftWarranty> Invalid(string reason) =>
        OperationResult<DraftWarranty>.Failure(new OperationError(
            $"Draft.Invalid.{reason}",
            "The draft contains invalid data."));
}

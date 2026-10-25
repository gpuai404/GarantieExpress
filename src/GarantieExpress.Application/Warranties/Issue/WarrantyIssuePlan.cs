using GarantieExpress.Application.Warranties.Drafts;
using GarantieExpress.Domain;

namespace GarantieExpress.Application.Warranties.Issue;

public sealed record WarrantyIssuePlan(
    WarrantyIssuance Issuance,
    WarrantyNumberPrefix NumberPrefix,
    DraftWarranty SourceDraft);

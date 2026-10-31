using GarantieExpress.Application.Common;
using GarantieExpress.Application.Warranties.Drafts;
using GarantieExpress.Domain;

namespace GarantieExpress.Application.Warranties.Issue;

public sealed record IssueWorkspaceData(
    StoreSettings Settings,
    IReadOnlyList<Category> Categories,
    DraftWarranty? Draft,
    OperationError? DraftWarning,
    IReadOnlyList<Product> Products,
    OperationError? ProductWarning);

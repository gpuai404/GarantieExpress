using GarantieExpress.Application.Warranties.Drafts;
using GarantieExpress.Domain;

namespace GarantieExpress.Application.Warranties.Issue;

public sealed record WarrantyIssueCommand(
    string RequestId,
    string InputHash,
    int Year,
    long SettingsVersion,
    IReadOnlyList<CategoryVersionExpectation> CategoryVersions,
    WarrantyNumberPrefix Prefix,
    WarrantyIssuance Warranty,
    WarrantySnapshot Snapshot,
    DraftWarranty SourceDraft);

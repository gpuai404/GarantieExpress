using GarantieExpress.Domain;

namespace GarantieExpress.Application.Warranties.Issue;

public sealed record WarrantyIssueResult(
    WarrantyRecord Warranty,
    WarrantySnapshot Snapshot);

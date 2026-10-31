using GarantieExpress.Domain;

namespace GarantieExpress.Application.Warranties.History;

public sealed record WarrantyHistoryPage(
    IReadOnlyList<WarrantyRecord> Warranties,
    long TotalCount,
    int PageIndex,
    int PageSize);

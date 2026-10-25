using System.Globalization;

namespace GarantieExpress.Application.Warranties.History;

public sealed record WarrantyHistoryPageRequest
{
    public const int DefaultPageSize = 50;
    public const int MaximumPageSize = 100;

    public WarrantyHistoryPageRequest(
        string? searchTerm,
        string cultureName,
        int pageIndex = 0,
        int pageSize = DefaultPageSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, MaximumPageSize);

        SearchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim();
        CultureName = CultureInfo.GetCultureInfo(cultureName).Name;
        PageIndex = pageIndex;
        PageSize = pageSize;
    }

    public string? SearchTerm { get; }
    public string CultureName { get; }
    public int PageIndex { get; }
    public int PageSize { get; }
}

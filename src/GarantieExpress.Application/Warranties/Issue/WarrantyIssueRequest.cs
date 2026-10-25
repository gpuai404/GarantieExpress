using GarantieExpress.Domain;

namespace GarantieExpress.Application.Warranties.Issue;

public sealed record WarrantyIssueRequest
{
    private WarrantyIssueRequest(string id, DateTimeOffset requestedAt)
    {
        Id = id;
        RequestedAt = requestedAt;
    }

    public string Id { get; }
    public DateTimeOffset RequestedAt { get; }

    public static WarrantyIssueRequest Create(DateTimeOffset requestedAt)
    {
        if (requestedAt == default)
            throw new ArgumentOutOfRangeException(nameof(requestedAt));

        return new WarrantyIssueRequest(Guid.NewGuid().ToString("N"), requestedAt);
    }

    public static DomainResult<WarrantyIssueRequest, WarrantyIssueRequestError> Restore(
        string? id,
        DateTimeOffset requestedAt)
    {
        if (!Guid.TryParseExact(id, "N", out _))
        {
            return DomainResult<WarrantyIssueRequest, WarrantyIssueRequestError>.Failure(
                WarrantyIssueRequestError.IdInvalid);
        }

        return requestedAt == default
            ? DomainResult<WarrantyIssueRequest, WarrantyIssueRequestError>.Failure(
                WarrantyIssueRequestError.RequestedAtInvalid)
            : DomainResult<WarrantyIssueRequest, WarrantyIssueRequestError>.Success(
                new WarrantyIssueRequest(id, requestedAt));
    }
}

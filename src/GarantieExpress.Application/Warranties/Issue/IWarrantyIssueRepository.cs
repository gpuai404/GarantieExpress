using GarantieExpress.Application.Common;
using GarantieExpress.Domain;

namespace GarantieExpress.Application.Warranties.Issue;

public interface IWarrantyIssueRepository
{
    Task<OperationResult<WarrantyIssueResult?>> GetResultAsync(
        string requestId,
        CancellationToken cancellationToken = default);

    Task<OperationResult<WarrantyRecord>> IssueAsync(
        WarrantyIssueCommand command,
        CancellationToken cancellationToken = default);
}

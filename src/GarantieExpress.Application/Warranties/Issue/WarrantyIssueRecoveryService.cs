using GarantieExpress.Application.Common;

namespace GarantieExpress.Application.Warranties.Issue;

public sealed class WarrantyIssueRecoveryService(IWarrantyIssueRepository issues)
{
    public Task<OperationResult<WarrantyIssueResult?>> RecoverAsync(
        string requestId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParseExact(requestId, "N", out _))
        {
            return Task.FromResult(OperationResult<WarrantyIssueResult?>.Failure(
                new OperationError(
                    "Warranty.InvalidRequest",
                    "The issuance request identity is invalid.")));
        }

        return issues.GetResultAsync(requestId, cancellationToken);
    }
}

using GarantieExpress.Application.Common;
using GarantieExpress.Application.Warranties.Issue;

namespace GarantieExpress.UI.Abstractions;

public interface IIssuanceRecoveryStore
{
    Task<OperationResult<WarrantyIssueRequest?>> GetAsync(
        CancellationToken cancellationToken = default);

    Task<OperationResult> SaveAsync(
        WarrantyIssueRequest request,
        CancellationToken cancellationToken = default);

    Task<OperationResult> ClearAsync(
        string requestId,
        CancellationToken cancellationToken = default);
}

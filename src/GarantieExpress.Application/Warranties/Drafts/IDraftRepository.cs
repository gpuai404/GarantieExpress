using GarantieExpress.Application.Common;
namespace GarantieExpress.Application.Warranties.Drafts;

public interface IDraftRepository
{
    Task<OperationResult<DraftWarranty?>> GetAsync(CancellationToken cancellationToken = default);
    Task<OperationResult<DraftWarranty>> SaveAsync(DraftWarranty draft, CancellationToken cancellationToken = default);
    Task<OperationResult> ClearAsync(
        string expectedId,
        long expectedVersion,
        CancellationToken cancellationToken = default);
}

using GarantieExpress.Application.Common;
using GarantieExpress.Domain;
namespace GarantieExpress.Application.Warranties.History;

public interface IWarrantyRepository
{
    Task<OperationResult<WarrantyHistoryPage>> SearchAsync(
        WarrantyHistoryPageRequest request,
        CancellationToken cancellationToken = default);
    Task<OperationResult<WarrantySnapshot?>> GetSnapshotAsync(
        string id,
        CancellationToken cancellationToken = default);
    Task<OperationResult<WarrantyRecord>> SaveStatusAsync(
        WarrantyRecord warranty,
        CancellationToken cancellationToken = default);
    Task<OperationResult> DeleteAsync(
        string number,
        long expectedVersion,
        CancellationToken cancellationToken = default);
}

using GarantieExpress.Application.Common;
using GarantieExpress.Domain;

namespace GarantieExpress.Application.Products;

public interface IProductRepository
{
    Task<OperationResult<IReadOnlyList<Product>>> GetAllAsync(
        CancellationToken cancellationToken = default);
    Task<OperationResult<Product>> SaveAsync(
        Product product,
        CancellationToken cancellationToken = default);
    Task<OperationResult> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}

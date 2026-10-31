using GarantieExpress.Application.Common;
using GarantieExpress.Domain;

namespace GarantieExpress.Application.Products;

public interface IProductRepository
{
    Task<OperationResult<IReadOnlyList<Product>>> GetAllAsync(
        CancellationToken cancellationToken = default);
    Task<OperationResult<Product>> AddAsync(
        Product product,
        CancellationToken cancellationToken = default);
    Task<OperationResult<Product>> UpdateAsync(
        Product product,
        Product expected,
        CancellationToken cancellationToken = default);
    Task<OperationResult> DeleteAsync(
        Product expected,
        CancellationToken cancellationToken = default);
}

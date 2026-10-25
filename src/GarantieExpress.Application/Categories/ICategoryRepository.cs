using GarantieExpress.Application.Common;
using GarantieExpress.Domain;

namespace GarantieExpress.Application.Categories;

public interface ICategoryRepository
{
    Task<OperationResult<IReadOnlyList<Category>>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<OperationResult<Category>> SaveAsync(Category category, CancellationToken cancellationToken = default);
}

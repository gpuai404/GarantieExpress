using GarantieExpress.Application.Common;
using GarantieExpress.Domain;

namespace GarantieExpress.Application.Settings;

public interface ISettingsRepository
{
    Task<OperationResult<StoreSettings?>> GetAsync(CancellationToken cancellationToken = default);
    Task<OperationResult<StoreSettings>> SaveAsync(StoreSettings settings, CancellationToken cancellationToken = default);
}

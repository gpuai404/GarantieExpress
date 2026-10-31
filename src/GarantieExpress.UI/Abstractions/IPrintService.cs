using GarantieExpress.Application.Common;

namespace GarantieExpress.UI.Abstractions;

public interface IPrintService
{
    Task<OperationResult<bool>> FitsPageAsync(
        CancellationToken cancellationToken = default);

    Task<OperationResult> PrintAsync(
        double widthMm,
        double heightMm,
        bool inkSaver,
        CancellationToken cancellationToken = default);
}

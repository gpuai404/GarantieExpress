using Microsoft.AspNetCore.Components.Forms;
using GarantieExpress.Application.Common;

namespace GarantieExpress.UI.Abstractions;

public interface IImageService
{
    Task<OperationResult<string>> DownscaleAsync(
        IBrowserFile file,
        CancellationToken cancellationToken = default);
}

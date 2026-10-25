using GarantieExpress.Application.Localization;
using GarantieExpress.Application.Common;
using GarantieExpress.UI.Abstractions;
using GarantieExpress.UI.Components.Warranty.Templates;

namespace GarantieExpress.UI.Components.Warranty;

public sealed class WarrantyPrintCoordinator(
    IPrintService printService,
    ILocalizationService localization)
{
    public async Task<OperationResult> CheckFitAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await printService.FitsPageAsync(cancellationToken);
        return result.Match(
            fits => fits
                ? OperationResult.Success()
                : OperationResult.Failure(new OperationError(
                    "Print.ContentOverflow",
                    localization["Validation_PrintOverflow"])),
            OperationResult.Failure);
    }

    public async Task<OperationResult> PrintAsync(
        WarrantyTemplate template,
        bool inkSaver,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);

        var fit = await CheckFitAsync(cancellationToken);
        if (fit is OperationResult.Failed) return fit;

        return await printService.PrintAsync(
            template.WidthMm,
            template.HeightMm,
            inkSaver,
            cancellationToken);
    }
}

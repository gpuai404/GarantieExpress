using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using GarantieExpress.Application.Localization;
using GarantieExpress.Application.Common;
using GarantieExpress.UI.Abstractions;
using GarantieExpress.UI.Localization;

namespace GarantieExpress.UI.Browser;

internal sealed class BrowserImageService(IJSRuntime jsRuntime, ILocalizationService localization) : IImageService, IAsyncDisposable
{
    private const long MaxUploadBytes = 2 * 1024 * 1024;
    private const long MaxProcessedBytes = 1024 * 1024;
    private static readonly HashSet<string> AllowedTypes = ["image/png", "image/jpeg", "image/webp"];
    private readonly BrowserJavaScriptModule _module = new(
        jsRuntime,
        "./_content/GarantieExpress.UI/js/image.js");

    public async Task<OperationResult<string>> DownscaleAsync(
        IBrowserFile file,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (!AllowedTypes.Contains(file.ContentType))
        {
            return Failure("Logo.UnsupportedType", "Error_LogoType");
        }

        if (file.Size > MaxUploadBytes)
        {
            return Failure("Logo.TooLarge", "Error_LogoSize");
        }

        try
        {
            await using var stream = file.OpenReadStream(MaxUploadBytes, cancellationToken);
            using var streamReference = new DotNetStreamReference(stream, leaveOpen: true);
            var module = await _module.GetAsync(cancellationToken);
            await using var resizedReference = await module.InvokeAsync<IJSStreamReference>(
                "downscaleImage",
                cancellationToken,
                streamReference,
                file.ContentType,
                800,
                320);
            await using var resizedStream = await resizedReference.OpenReadStreamAsync(
                MaxProcessedBytes,
                cancellationToken);
            using var buffer = new MemoryStream();
            await resizedStream.CopyToAsync(buffer, cancellationToken);
            if (buffer.Length == 0)
                return Failure("Logo.ProcessingFailed", "Error_LogoProcessing");

            var resized = $"data:image/png;base64,{Convert.ToBase64String(buffer.ToArray())}";
            return OperationResult<string>.Success(resized);
        }
        catch (Exception exception) when (
            exception is IOException or JSException or JSDisconnectedException)
        {
            return Failure("Logo.ProcessingFailed", "Error_LogoProcessing");
        }
    }

    private OperationResult<string> Failure(string code, string resourceKey) =>
        OperationResult<string>.Failure(new OperationError(code, localization[resourceKey]));

    public ValueTask DisposeAsync() => _module.DisposeAsync();
}

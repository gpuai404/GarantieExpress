using GarantieExpress.Application.Common;

namespace GarantieExpress.UI.Abstractions;

public interface ILanguagePreferenceStore
{
    Task<OperationResult<string?>> GetLanguageAsync(
        CancellationToken cancellationToken = default);

    Task<OperationResult> ApplyLanguageAsync(
        string language,
        bool persist,
        CancellationToken cancellationToken = default);
}

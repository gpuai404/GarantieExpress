using GarantieExpress.Application.Common;

namespace GarantieExpress.Application.Warranties.Issue;

/// <summary>
/// Verifies whether the application is currently allowed to issue new warranties.
/// Existing warranty records and reprints are intentionally outside this gate.
/// </summary>
public interface IWarrantyIssuanceGate
{
    Task<OperationResult> EnsureEnabledAsync(
        CancellationToken cancellationToken = default);
}

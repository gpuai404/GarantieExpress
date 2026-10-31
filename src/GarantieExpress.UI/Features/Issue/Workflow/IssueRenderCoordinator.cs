using GarantieExpress.Application.Localization;
using GarantieExpress.Application.Common;

namespace GarantieExpress.UI.Features.Issue.Workflow;

public sealed class IssueRenderCoordinator(ILocalizationService localization)
{
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromSeconds(5);
    private TaskCompletionSource? _pendingRender;

    public async Task<OperationResult> WaitForRenderAsync(
        Action requestRender,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestRender);

        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (Interlocked.CompareExchange(ref _pendingRender, completion, null) is not null)
        {
            return OperationResult.Failure(new OperationError(
                "Print.RenderAlreadyPending",
                localization["Issue_PrintFailed"]));
        }

        requestRender();
        try
        {
            await completion.Task.WaitAsync(RenderTimeout, cancellationToken);
            return OperationResult.Success();
        }
        catch (TimeoutException)
        {
            return OperationResult.Failure(new OperationError(
                "Print.RenderTimeout",
                localization["Issue_PrintFailed"]));
        }
        finally
        {
            Interlocked.CompareExchange(ref _pendingRender, null, completion);
        }
    }

    public void NotifyRendered() =>
        Interlocked.Exchange(ref _pendingRender, null)?.TrySetResult();
}

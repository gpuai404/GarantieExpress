using Microsoft.AspNetCore.Components;
using GarantieExpress.Application.Localization;
using GarantieExpress.UI.Features.Issue.Workflow;
using GarantieExpress.UI.Services;

namespace GarantieExpress.UI.Features.Issue;

public partial class Issue : IAsyncDisposable
{
    [Inject] private ILocalizationService Localization { get; set; } = null!;
    [Inject] private IssueWorkflowFactory WorkflowFactory { get; set; } = null!;
    [Inject] private PendingChangesCoordinator PendingChanges { get; set; } = null!;
    private IssueWorkflow Workflow { get; set; } = null!;
    private IDisposable? _pendingChangesRegistration;

    protected override async Task OnInitializedAsync()
    {
        Workflow = WorkflowFactory.Create();
        Workflow.StateChanged += OnWorkflowStateChanged;
        _pendingChangesRegistration = PendingChanges.Register(Workflow.PrepareToLeaveAsync);
        await Workflow.InitializeAsync();
    }

    private void OnWorkflowStateChanged() => _ = InvokeAsync(StateHasChanged);

    protected override void OnAfterRender(bool firstRender) => Workflow.NotifyRendered();

    public async ValueTask DisposeAsync()
    {
        Workflow.StateChanged -= OnWorkflowStateChanged;
        _pendingChangesRegistration?.Dispose();
        await Workflow.DisposeAsync();
    }
}

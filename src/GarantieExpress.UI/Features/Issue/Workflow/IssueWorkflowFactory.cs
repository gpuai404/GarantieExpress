using Microsoft.Extensions.Logging;
using GarantieExpress.Application.Localization;
using GarantieExpress.Application.Warranties.Drafts;
using GarantieExpress.Application.Warranties.Issue;
using GarantieExpress.UI.Abstractions;
using GarantieExpress.UI.Components.Warranty;
using GarantieExpress.UI.Components.Warranty.Presentation;
using GarantieExpress.UI.Components.Warranty.Templates;

namespace GarantieExpress.UI.Features.Issue.Workflow;

public sealed class IssueWorkflowFactory(
    IssueWorkspaceService workspaceService,
    WarrantyIssueService issuer,
    WarrantyDraftService draftService,
    WarrantyIssueRecoveryService recoveryService,
    IIssuanceRecoveryStore recoveryStore,
    WarrantyDisplayModelBuilder displayModels,
    TemplateRegistry templates,
    WarrantyPrintCoordinator printer,
    ILocalizationService localization,
    TimeProvider timeProvider,
    ILoggerFactory loggerFactory)
{
    public IssueWorkflow Create() => new(
        workspaceService,
        issuer,
        new IssueDraftCoordinator(
            draftService,
            localization,
            loggerFactory.CreateLogger<IssueDraftCoordinator>()),
        new IssueRenderCoordinator(localization),
        new WarrantyIssueRecoveryCoordinator(recoveryService, recoveryStore),
        displayModels,
        templates,
        printer,
        localization,
        timeProvider);
}

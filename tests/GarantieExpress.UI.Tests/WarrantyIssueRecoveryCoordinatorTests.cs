using GarantieExpress.Application.Common;
using GarantieExpress.Application.Warranties.Issue;
using GarantieExpress.Domain;
using GarantieExpress.UI.Abstractions;
using GarantieExpress.UI.Features.Issue.Workflow;
using Xunit;

namespace GarantieExpress.UI.Tests;

public sealed class WarrantyIssueRecoveryCoordinatorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CompleteAsync_clears_the_prepared_request()
    {
        var store = new RecoveryStore();
        var coordinator = CreateCoordinator(store);
        await coordinator.InitializeAsync();
        var prepared = Assert.IsType<OperationResult<WarrantyIssueRequest>.Succeeded>(
            await coordinator.PrepareAsync(Now));

        var result = await coordinator.CompleteAsync();

        Assert.IsType<OperationResult.Succeeded>(result);
        Assert.True(coordinator.IsReady);
        Assert.False(coordinator.HasPendingRequest);
        Assert.Equal(1, store.ClearCalls);
        Assert.Equal(prepared.Value.Id, store.ClearedRequestId);
    }

    [Fact]
    public async Task InitializeAsync_retains_an_unresolved_valid_request()
    {
        var request = WarrantyIssueRequest.Create(Now);
        var store = new RecoveryStore { StoredRequest = request };
        var coordinator = CreateCoordinator(store);

        var result = await coordinator.InitializeAsync();

        var succeeded = Assert.IsType<
            OperationResult<WarrantyIssueResult?>.Succeeded>(result);
        Assert.Null(succeeded.Value);
        Assert.True(coordinator.IsReady);
        Assert.True(coordinator.HasPendingRequest);
        Assert.Equal(0, store.ClearCalls);
    }

    [Fact]
    public async Task PrepareAsync_persists_and_reuses_the_request_identity()
    {
        var store = new RecoveryStore();
        var coordinator = CreateCoordinator(store);
        await coordinator.InitializeAsync();

        var first = Assert.IsType<OperationResult<WarrantyIssueRequest>.Succeeded>(
            await coordinator.PrepareAsync(Now));
        var second = Assert.IsType<OperationResult<WarrantyIssueRequest>.Succeeded>(
            await coordinator.PrepareAsync(Now.AddMinutes(1)));

        Assert.Equal(first.Value.Id, second.Value.Id);
        Assert.Equal(Now, second.Value.RequestedAt);
        Assert.Equal(2, store.SaveCalls);
        Assert.Equal(first.Value, store.SavedRequest);
    }

    [Fact]
    public async Task InitializeAsync_keeps_the_coordinator_unready_when_storage_fails()
    {
        var store = new RecoveryStore
        {
            GetError = new OperationError("Test.Storage", "Storage failed")
        };
        var coordinator = CreateCoordinator(store);

        var result = await coordinator.InitializeAsync();

        var failed = Assert.IsType<
            OperationResult<WarrantyIssueResult?>.Failed>(result);
        Assert.Equal("Test.Storage", failed.Details.Code);
        Assert.False(coordinator.IsReady);
    }

    private static WarrantyIssueRecoveryCoordinator CreateCoordinator(RecoveryStore store) =>
        new(
            new WarrantyIssueRecoveryService(new EmptyIssueRepository()),
            store);

    private sealed class RecoveryStore : IIssuanceRecoveryStore
    {
        public WarrantyIssueRequest? StoredRequest { get; init; }
        public WarrantyIssueRequest? SavedRequest { get; private set; }
        public OperationError? GetError { get; init; }
        public int SaveCalls { get; private set; }
        public int ClearCalls { get; private set; }
        public string? ClearedRequestId { get; private set; }

        public Task<OperationResult<WarrantyIssueRequest?>> GetAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(GetError is null
                ? OperationResult<WarrantyIssueRequest?>.Success(StoredRequest)
                : OperationResult<WarrantyIssueRequest?>.Failure(GetError));

        public Task<OperationResult> SaveAsync(
            WarrantyIssueRequest request,
            CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            SavedRequest = request;
            return Task.FromResult(OperationResult.Success());
        }

        public Task<OperationResult> ClearAsync(
            string requestId,
            CancellationToken cancellationToken = default)
        {
            ClearCalls++;
            ClearedRequestId = requestId;
            return Task.FromResult(OperationResult.Success());
        }
    }

    private sealed class EmptyIssueRepository : IWarrantyIssueRepository
    {
        public Task<OperationResult<WarrantyIssueResult?>> GetResultAsync(
            string requestId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult<WarrantyIssueResult?>.Success(null));

        public Task<OperationResult<WarrantyRecord>> IssueAsync(
            WarrantyIssueCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

}

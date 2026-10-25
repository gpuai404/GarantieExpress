namespace GarantieExpress.Infrastructure.Storage;

public sealed class DatabaseOperationGate
{
    private readonly object _sync = new();
    private TaskCompletionSource _drained = CompletedSource();
    private bool _accepting = true;
    private int _activeOperations;

    internal IDisposable? TryEnter()
    {
        lock (_sync)
        {
            if (!_accepting) return null;
            if (_activeOperations++ == 0)
                _drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return new Lease(this);
        }
    }

    public async Task StopAcceptingAndDrainAsync(CancellationToken cancellationToken = default)
    {
        Task drained;
        lock (_sync)
        {
            _accepting = false;
            drained = _activeOperations == 0 ? Task.CompletedTask : _drained.Task;
        }

        await drained.WaitAsync(cancellationToken);
    }

    private void Exit()
    {
        lock (_sync)
        {
            if (--_activeOperations == 0) _drained.TrySetResult();
        }
    }

    private static TaskCompletionSource CompletedSource()
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.SetResult();
        return source;
    }

    private sealed class Lease(DatabaseOperationGate owner) : IDisposable
    {
        private DatabaseOperationGate? _owner = owner;

        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Exit();
    }
}

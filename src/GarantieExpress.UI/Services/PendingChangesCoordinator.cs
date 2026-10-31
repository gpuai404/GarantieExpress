using GarantieExpress.Application.Common;

namespace GarantieExpress.UI.Services;

public sealed class PendingChangesCoordinator
{
    private readonly List<Func<Task<OperationResult>>> _participants = [];

    public IDisposable Register(Func<Task<OperationResult>> save)
    {
        ArgumentNullException.ThrowIfNull(save);
        _participants.Add(save);
        return new Registration(this, save);
    }

    public async Task<OperationResult> FlushAsync()
    {
        foreach (var save in _participants.ToArray())
        {
            var result = await save();
            if (result is OperationResult.Failed) return result;
        }

        return OperationResult.Success();
    }

    private sealed class Registration(
        PendingChangesCoordinator owner,
        Func<Task<OperationResult>> save) : IDisposable
    {
        public void Dispose() => owner._participants.Remove(save);
    }
}

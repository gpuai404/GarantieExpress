namespace GarantieExpress.Application.Common;

public abstract class OperationResult
{
    private OperationResult() { }

    public sealed class Succeeded : OperationResult
    {
        internal Succeeded() { }

        protected override TResult MatchCore<TResult>(
            Func<TResult> onSuccess,
            Func<OperationError, TResult> onFailure) => onSuccess();
    }

    public sealed class Failed : OperationResult
    {
        internal Failed(OperationError error)
        {
            Details = error;
        }

        public OperationError Details { get; }
        public string Error => Details.Message;

        protected override TResult MatchCore<TResult>(
            Func<TResult> onSuccess,
            Func<OperationError, TResult> onFailure) => onFailure(Details);
    }

    public static OperationResult Success() => new Succeeded();

    public static OperationResult Failure(OperationError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Failed(error);
    }

    public TResult Match<TResult>(Func<TResult> onSuccess, Func<OperationError, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return MatchCore(onSuccess, onFailure);
    }

    protected abstract TResult MatchCore<TResult>(
        Func<TResult> onSuccess,
        Func<OperationError, TResult> onFailure);
}

public abstract class OperationResult<T>
{
    private OperationResult() { }

    public sealed class Succeeded : OperationResult<T>
    {
        internal Succeeded(T value) => Value = value;

        public T Value { get; }

        protected override TResult MatchCore<TResult>(
            Func<T, TResult> onSuccess,
            Func<OperationError, TResult> onFailure) => onSuccess(Value);
    }

    public sealed class Failed : OperationResult<T>
    {
        internal Failed(OperationError error)
        {
            Details = error;
        }

        public OperationError Details { get; }
        public string Error => Details.Message;

        protected override TResult MatchCore<TResult>(
            Func<T, TResult> onSuccess,
            Func<OperationError, TResult> onFailure) => onFailure(Details);
    }

    public static OperationResult<T> Success(T value) => new Succeeded(value);

    public static OperationResult<T> Failure(OperationError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Failed(error);
    }

    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<OperationError, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return MatchCore(onSuccess, onFailure);
    }

    protected abstract TResult MatchCore<TResult>(
        Func<T, TResult> onSuccess,
        Func<OperationError, TResult> onFailure);
}

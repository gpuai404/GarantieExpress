namespace GarantieExpress.Domain;

public abstract class DomainResult<TValue, TError>
    where TValue : notnull
    where TError : notnull
{
    private DomainResult() { }

    public abstract bool IsSuccess { get; }

    public TResult Match<TResult>(
        Func<TValue, TResult> onSuccess,
        Func<TError, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return MatchCore(onSuccess, onFailure);
    }

    public DomainResult<TNext, TError> Map<TNext>(Func<TValue, TNext> map)
        where TNext : notnull
    {
        ArgumentNullException.ThrowIfNull(map);
        return Match(
            value => DomainResult<TNext, TError>.Success(map(value)),
            DomainResult<TNext, TError>.Failure);
    }

    public DomainResult<TNext, TError> Bind<TNext>(
        Func<TValue, DomainResult<TNext, TError>> bind)
        where TNext : notnull
    {
        ArgumentNullException.ThrowIfNull(bind);
        return Match(bind, DomainResult<TNext, TError>.Failure);
    }

    protected abstract TResult MatchCore<TResult>(
        Func<TValue, TResult> onSuccess,
        Func<TError, TResult> onFailure);

    public static DomainResult<TValue, TError> Success(TValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Succeeded(value);
    }

    public static DomainResult<TValue, TError> Failure(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Failed(error);
    }

    public sealed class Succeeded : DomainResult<TValue, TError>
    {
        internal Succeeded(TValue value) => Value = value;

        public TValue Value { get; }
        public override bool IsSuccess => true;

        protected override TResult MatchCore<TResult>(
            Func<TValue, TResult> onSuccess,
            Func<TError, TResult> onFailure) => onSuccess(Value);
    }

    public sealed class Failed : DomainResult<TValue, TError>
    {
        internal Failed(TError error) => Error = error;

        public TError Error { get; }
        public override bool IsSuccess => false;

        protected override TResult MatchCore<TResult>(
            Func<TValue, TResult> onSuccess,
            Func<TError, TResult> onFailure) => onFailure(Error);
    }
}

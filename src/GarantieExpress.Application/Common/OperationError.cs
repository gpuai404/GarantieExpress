namespace GarantieExpress.Application.Common;

public sealed record OperationError
{
    public OperationError(string code, string message, string? field = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(message);
        Code = code;
        Message = message;
        Field = field;
    }

    public string Code { get; }
    public string Message { get; }
    public string? Field { get; }
}

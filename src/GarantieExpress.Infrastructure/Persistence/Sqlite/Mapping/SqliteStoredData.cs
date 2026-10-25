using System.Text.Json;

namespace GarantieExpress.Infrastructure.Persistence.Sqlite.Mapping;

internal static class SqliteStoredData
{
    private static JsonSerializerOptions JsonOptions { get; } =
        new(JsonSerializerDefaults.Web);

    internal static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, JsonOptions);

    internal static T Deserialize<T>(string json) => Decode(() =>
        JsonSerializer.Deserialize<T>(json, JsonOptions)
        ?? throw new JsonException($"Could not deserialize {typeof(T).Name}."));

    internal static T Decode<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch (Exception exception) when (
            exception is JsonException
                or FormatException
                or InvalidCastException
                or OverflowException)
        {
            throw new InvalidStoredDataException(exception);
        }
    }
}

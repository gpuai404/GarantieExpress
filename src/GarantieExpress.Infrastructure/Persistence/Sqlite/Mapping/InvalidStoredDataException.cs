namespace GarantieExpress.Infrastructure.Persistence.Sqlite.Mapping;

internal sealed class InvalidStoredDataException(Exception innerException)
    : Exception("Persisted data could not be decoded.", innerException);

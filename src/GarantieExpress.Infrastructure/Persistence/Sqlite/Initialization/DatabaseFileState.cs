using GarantieExpress.Infrastructure.Storage;

namespace GarantieExpress.Infrastructure.Persistence.Sqlite.Initialization;

internal sealed class DatabaseFileState
{
    private DatabaseFileState(bool markerExists, bool databaseFileExisted)
    {
        MarkerExists = markerExists;
        DatabaseFileExisted = databaseFileExisted;
    }

    internal bool MarkerExists { get; }
    internal bool DatabaseFileExisted { get; }

    internal static DatabaseFileState Capture(ApplicationPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return new(paths.IsInitialized, File.Exists(paths.DatabasePath));
    }

    internal void EnsureDatabaseFileIsPresent()
    {
        if (MarkerExists && !DatabaseFileExisted)
        {
            throw new InvalidOperationException(
                "The GarantieExpress database is missing from an established installation.");
        }
    }
}

namespace GarantieExpress.Infrastructure.Storage;

public sealed class ApplicationPaths
{
    public ApplicationPaths() : this(GetDataDirectory())
    {
    }

    public ApplicationPaths(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        DataDirectory = Path.GetFullPath(dataDirectory);
        Directory.CreateDirectory(DataDirectory);
        DatabasePath = Path.Combine(DataDirectory, "garantie-express.db");
        InitializationMarkerPath = Path.Combine(DataDirectory, "installation.initialized");
    }

    public string DataDirectory { get; }
    public string DatabasePath { get; }
    public string InitializationMarkerPath { get; }

    public bool IsInitialized => File.Exists(InitializationMarkerPath);

    public void MarkInitialized()
    {
        using var stream = new FileStream(
            InitializationMarkerPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 64,
            FileOptions.WriteThrough);
        using var writer = new StreamWriter(stream);
        writer.WriteLine("GarantieExpress");
        writer.Flush();
        stream.Flush(flushToDisk: true);
    }

    private static string GetDataDirectory()
    {
        if (OperatingSystem.IsMacOS())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support", "GarantieExpress");

        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localData)) return Path.Combine(localData, "GarantieExpress");

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "GarantieExpress");
    }
}

namespace GarantieExpress.Host.Updates;

public sealed record ApplicationUpdateStatus(
    string InstalledVersion,
    string? LatestVersion = null,
    string? MinimumSupportedVersion = null,
    bool IsUpdateRequired = false,
    Uri? DownloadUri = null,
    string? Message = null)
{
    public bool IsUpdateAvailable => LatestVersion is not null;
}

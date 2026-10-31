namespace GarantieExpress.Host.Updates;

internal sealed record UpdateCheckOptions(
    Uri? ApiUri,
    TimeSpan Timeout,
    Version InstalledVersion)
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    public static UpdateCheckOptions FromConfiguration(
        IConfiguration configuration,
        Version installedVersion)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(installedVersion);

        var configuredUrl = configuration["GarantieExpress:Updates:ApiUrl"];
        Uri? apiUri = null;
        if (!string.IsNullOrWhiteSpace(configuredUrl))
        {
            if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out apiUri)
                || apiUri.Scheme != Uri.UriSchemeHttps
                || !string.IsNullOrEmpty(apiUri.UserInfo)
                || !string.IsNullOrEmpty(apiUri.Fragment))
            {
                throw new InvalidOperationException(
                    "GarantieExpress:Updates:ApiUrl must be an HTTPS URL without credentials or a fragment.");
            }
        }

        var timeoutSeconds = configuration.GetValue<int?>(
            "GarantieExpress:Updates:TimeoutSeconds");
        if (timeoutSeconds is < 1 or > 30)
        {
            throw new InvalidOperationException(
                "GarantieExpress:Updates:TimeoutSeconds must be between 1 and 30.");
        }

        return new UpdateCheckOptions(
            apiUri,
            timeoutSeconds is null
                ? DefaultTimeout
                : TimeSpan.FromSeconds(timeoutSeconds.Value),
            installedVersion);
    }
}

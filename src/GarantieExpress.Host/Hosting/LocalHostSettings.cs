namespace GarantieExpress.Host.Hosting;

internal sealed record LocalHostSettings(Uri Endpoint, bool LaunchBrowser)
{
    private const string DefaultUrl = "http://127.0.0.1:5248";

    public static LocalHostSettings FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configuredUrl = configuration["GarantieExpress:Url"] ?? DefaultUrl;
        if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var endpoint)
            || !IsSupportedEndpoint(endpoint))
        {
            throw new InvalidOperationException(
                "GarantieExpress:Url must be an HTTP loopback URL with a fixed port and no path, query, fragment, or credentials.");
        }

        return new LocalHostSettings(
            endpoint,
            configuration.GetValue("GarantieExpress:LaunchBrowser", true));
    }

    public static bool IsSupportedEndpoint(Uri endpoint) =>
        endpoint.IsAbsoluteUri
        && endpoint.IsLoopback
        && endpoint.Scheme == Uri.UriSchemeHttp
        && endpoint.Port > 0
        && endpoint.AbsolutePath == "/"
        && string.IsNullOrEmpty(endpoint.UserInfo)
        && string.IsNullOrEmpty(endpoint.Query)
        && string.IsNullOrEmpty(endpoint.Fragment);
}

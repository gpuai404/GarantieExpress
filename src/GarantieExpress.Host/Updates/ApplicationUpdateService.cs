using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.WebUtilities;

namespace GarantieExpress.Host.Updates;

internal sealed class ApplicationUpdateService(
    IHttpClientFactory httpClientFactory,
    UpdateCheckOptions options,
    ILogger<ApplicationUpdateService> logger)
{
    private const string HttpClientName = "GarantieExpress.Updates";
    private static readonly Uri TrustedDownloadUri = new(
        "https://github.com/gpuai404/GarantieExpress/releases/latest");
    private readonly SemaphoreSlim _checkLock = new(1, 1);
    private ApplicationUpdateStatus _status = new(Normalize(options.InstalledVersion));

    public ApplicationUpdateStatus Status => Volatile.Read(ref _status);

    public event Action? StatusChanged;

    public async Task CheckAsync(CancellationToken cancellationToken = default)
    {
        if (options.ApiUri is null)
        {
            logger.LogInformation("The application update check is disabled because no API URL is configured.");
            return;
        }

        await _checkLock.WaitAsync(cancellationToken);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(options.Timeout);

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                BuildRequestUri(options.ApiUri, Status.InstalledVersion));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
            request.Headers.UserAgent.ParseAdd(
                $"GarantieExpress/{Status.InstalledVersion}");

            var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "The application update API returned HTTP {StatusCode}.",
                    (int)response.StatusCode);
                return;
            }

            var manifest = await response.Content.ReadFromJsonAsync<UpdateManifest>(
                cancellationToken: timeout.Token);
            if (!TryEvaluate(manifest, options.InstalledVersion, out var status))
            {
                logger.LogWarning("The application update API returned an invalid manifest.");
                return;
            }

            var previousStatus = Interlocked.Exchange(ref _status, status);
            if (status != previousStatus) StatusChanged?.Invoke();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "The application update check timed out after {TimeoutSeconds} seconds.",
                options.Timeout.TotalSeconds);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "The application update API could not be reached.");
        }
        catch (NotSupportedException exception)
        {
            logger.LogWarning(exception, "The application update API response was not supported.");
        }
        catch (System.Text.Json.JsonException exception)
        {
            logger.LogWarning(exception, "The application update API returned invalid JSON.");
        }
        finally
        {
            _checkLock.Release();
        }
    }

    internal static bool TryEvaluate(
        UpdateManifest? manifest,
        Version installedVersion,
        out ApplicationUpdateStatus status)
    {
        var installed = Normalize(installedVersion);
        status = new ApplicationUpdateStatus(installed);

        if (manifest is null
            || manifest.SchemaVersion != 2
            || !TryParseStableVersion(manifest.LatestVersion, out var latestVersion)
            || !TryParseStableVersion(
                manifest.MinimumSupportedVersion,
                out var minimumSupportedVersion)
            || minimumSupportedVersion > latestVersion)
        {
            return false;
        }

        if (latestVersion <= installedVersion) return true;

        var message = string.IsNullOrWhiteSpace(manifest.Message)
            ? null
            : manifest.Message.Trim();
        if (message?.Length > 500) return false;

        status = new ApplicationUpdateStatus(
            installed,
            Normalize(latestVersion),
            Normalize(minimumSupportedVersion),
            installedVersion < minimumSupportedVersion,
            TrustedDownloadUri,
            message);
        return true;
    }

    private static Uri BuildRequestUri(Uri apiUri, string installedVersion)
    {
        var values = new Dictionary<string, string?>
        {
            ["currentVersion"] = installedVersion,
            ["platform"] = GetPlatform(),
            ["architecture"] = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()
        };
        return new Uri(QueryHelpers.AddQueryString(apiUri.AbsoluteUri, values));
    }

    private static string GetPlatform()
    {
        if (OperatingSystem.IsWindows()) return "windows";
        if (OperatingSystem.IsMacOS()) return "macos";
        if (OperatingSystem.IsLinux()) return "linux";
        return "unknown";
    }

    private static bool TryParseStableVersion(string? value, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(value)) return false;

        var parts = value.Split('.');
        if (parts.Length != 3
            || parts.Any(part => part.Length == 0
                || (part.Length > 1 && part[0] == '0')
                || !part.All(char.IsAsciiDigit)))
        {
            return false;
        }

        if (!Version.TryParse(value, out var parsed)) return false;
        version = parsed;
        return true;
    }

    private static string Normalize(Version version) =>
        $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";

    internal sealed record UpdateManifest(
        string? LatestVersion,
        string? MinimumSupportedVersion,
        string? Message,
        int SchemaVersion);
}

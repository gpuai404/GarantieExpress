using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.WebUtilities;
using GarantieExpress.Application.Common;
using GarantieExpress.Application.Warranties.Issue;

namespace GarantieExpress.Host.Updates;

internal sealed class ApplicationUpdateService(
    IHttpClientFactory httpClientFactory,
    UpdateCheckOptions options,
    ILogger<ApplicationUpdateService> logger) : IWarrantyIssuanceGate
{
    private const string HttpClientName = "GarantieExpress.Updates";
    private const int MinimumCheckIntervalSeconds = 5 * 60;
    private const int MaximumCheckIntervalSeconds = 7 * 24 * 60 * 60;
    private static readonly Uri TrustedDownloadUri = new(
        "https://github.com/gpuai404/GarantieExpress/releases/latest");
    private readonly SemaphoreSlim _checkLock = new(1, 1);
    private long _nextCheckIntervalTicks;
    private int _warrantyIssuanceEnabled;
    private ApplicationUpdateStatus _status = new(Normalize(options.InstalledVersion));

    public ApplicationUpdateStatus Status => Volatile.Read(ref _status);

    public TimeSpan? NextCheckInterval
    {
        get
        {
            var ticks = Volatile.Read(ref _nextCheckIntervalTicks);
            return ticks == 0 ? null : TimeSpan.FromTicks(ticks);
        }
    }

    public event Action? StatusChanged;

    public async Task<OperationResult> EnsureEnabledAsync(
        CancellationToken cancellationToken = default)
    {
        if (options.ApiUri is null)
        {
            logger.LogDebug(
                "Remote warranty issuance control is disabled because no update API URL is configured.");
            return OperationResult.Success();
        }

        var check = await CheckAsync(cancellationToken);
        if (check is OperationResult.Failed)
            return check;

        return Volatile.Read(ref _warrantyIssuanceEnabled) == 1
            ? OperationResult.Success()
            : OperationResult.Failure(new OperationError(
                "Warranty.IssuanceDisabled",
                "Warranty issuance is currently disabled."));
    }

    public async Task<OperationResult> CheckAsync(
        CancellationToken cancellationToken = default)
    {
        if (options.ApiUri is null)
        {
            logger.LogInformation("The application update check is disabled because no API URL is configured.");
            return OperationResult.Failure(UpdateServiceUnavailable());
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
                return OperationResult.Failure(UpdateServiceUnavailable());
            }

            var manifest = await response.Content.ReadFromJsonAsync<UpdateManifest>(
                cancellationToken: timeout.Token);
            if (!TryEvaluate(manifest, options.InstalledVersion, out var status))
            {
                logger.LogWarning("The application update API returned an invalid manifest.");
                return OperationResult.Failure(UpdateServiceUnavailable());
            }

            if (!TryGetWarrantyIssuanceEnabled(manifest, out var warrantyIssuanceEnabled))
            {
                logger.LogWarning(
                    "The application update API did not provide a valid warranty issuance state.");
                return OperationResult.Failure(UpdateServiceUnavailable());
            }

            if (TryGetNextCheckInterval(manifest, out var nextCheckInterval))
            {
                Volatile.Write(ref _nextCheckIntervalTicks, nextCheckInterval.Ticks);
            }
            else
            {
                logger.LogWarning(
                    "The application update API returned an invalid or missing next check interval; retaining the previous interval.");
            }

            Volatile.Write(
                ref _warrantyIssuanceEnabled,
                warrantyIssuanceEnabled ? 1 : 0);

            var previousStatus = Interlocked.Exchange(ref _status, status);
            if (status != previousStatus) StatusChanged?.Invoke();
            return OperationResult.Success();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "The application update check timed out after {TimeoutSeconds} seconds.",
                options.Timeout.TotalSeconds);
            return UpdateServiceFailure();
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "The application update API could not be reached.");
            return UpdateServiceFailure();
        }
        catch (NotSupportedException exception)
        {
            logger.LogWarning(exception, "The application update API response was not supported.");
            return UpdateServiceFailure();
        }
        catch (System.Text.Json.JsonException exception)
        {
            logger.LogWarning(exception, "The application update API returned invalid JSON.");
            return UpdateServiceFailure();
        }
        finally
        {
            _checkLock.Release();
        }
    }

    private static OperationResult UpdateServiceFailure() =>
        OperationResult.Failure(UpdateServiceUnavailable());

    private static OperationError UpdateServiceUnavailable() => new(
        "Warranty.IssuanceCheckUnavailable",
        "Warranty issuance could not be verified. Check your internet connection and try again.");

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

    internal static bool TryGetNextCheckInterval(
        UpdateManifest? manifest,
        out TimeSpan interval)
    {
        interval = default;
        if (manifest?.NextCheckAfterSeconds is not { } seconds
            || seconds < MinimumCheckIntervalSeconds
            || seconds > MaximumCheckIntervalSeconds)
        {
            return false;
        }

        interval = TimeSpan.FromSeconds(seconds);
        return true;
    }

    internal static bool TryGetWarrantyIssuanceEnabled(
        UpdateManifest? manifest,
        out bool enabled)
    {
        enabled = false;
        if (manifest?.WarrantyIssuanceEnabled is not bool value) return false;

        enabled = value;
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
        int SchemaVersion,
        int? NextCheckAfterSeconds = null,
        bool? WarrantyIssuanceEnabled = null);
}

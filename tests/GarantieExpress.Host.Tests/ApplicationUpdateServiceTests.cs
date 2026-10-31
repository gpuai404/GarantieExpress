using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using GarantieExpress.Application.Common;
using GarantieExpress.Host.Updates;
using Xunit;

namespace GarantieExpress.Host.Tests;

public sealed class ApplicationUpdateServiceTests
{
    [Fact]
    public void Remote_manifest_controls_the_next_check_interval()
    {
        var manifest = new ApplicationUpdateService.UpdateManifest(
            LatestVersion: "1.0.0",
            MinimumSupportedVersion: "1.0.0",
            Message: null,
            SchemaVersion: 2,
            NextCheckAfterSeconds: 3721);

        var valid = ApplicationUpdateService.TryGetNextCheckInterval(
            manifest,
            out var interval);

        Assert.True(valid);
        Assert.Equal(
            TimeSpan.FromSeconds(3721),
            interval);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Remote_manifest_controls_warranty_issuance(bool expected)
    {
        var manifest = new ApplicationUpdateService.UpdateManifest(
            LatestVersion: "1.0.0",
            MinimumSupportedVersion: "1.0.0",
            Message: null,
            SchemaVersion: 2,
            WarrantyIssuanceEnabled: expected);

        var valid = ApplicationUpdateService.TryGetWarrantyIssuanceEnabled(
            manifest,
            out var actual);

        Assert.True(valid);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Remote_manifest_rejects_a_missing_warranty_issuance_state()
    {
        var manifest = new ApplicationUpdateService.UpdateManifest(
            "1.0.0",
            "1.0.0",
            Message: null,
            SchemaVersion: 2);

        var valid = ApplicationUpdateService.TryGetWarrantyIssuanceEnabled(
            manifest,
            out _);

        Assert.False(valid);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Issuance_gate_enforces_the_live_manifest_state(bool enabled)
    {
        var manifest = JsonSerializer.Serialize(new
        {
            schemaVersion = 2,
            latestVersion = "1.0.0",
            minimumSupportedVersion = "1.0.0",
            nextCheckAfterSeconds = 3600,
            warrantyIssuanceEnabled = enabled
        });
        using var client = new HttpClient(new ManifestHandler(manifest));
        var service = new ApplicationUpdateService(
            new FixedHttpClientFactory(client),
            new UpdateCheckOptions(
                new Uri("https://updates.test/update.json"),
                TimeSpan.FromSeconds(5),
                new Version(1, 0, 0)),
            NullLogger<ApplicationUpdateService>.Instance);

        var result = await service.EnsureEnabledAsync();

        if (enabled)
        {
            Assert.IsType<OperationResult.Succeeded>(result);
        }
        else
        {
            var failure = Assert.IsType<OperationResult.Failed>(result);
            Assert.Equal("Warranty.IssuanceDisabled", failure.Details.Code);
        }
    }

    [Fact]
    public async Task Issuance_gate_allows_local_mode_when_remote_control_is_not_configured()
    {
        var service = new ApplicationUpdateService(
            new FixedHttpClientFactory(new HttpClient()),
            new UpdateCheckOptions(
                ApiUri: null,
                TimeSpan.FromSeconds(5),
                new Version(1, 0, 0)),
            NullLogger<ApplicationUpdateService>.Instance);

        var result = await service.EnsureEnabledAsync();

        Assert.IsType<OperationResult.Succeeded>(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(299)]
    [InlineData(604801)]
    public void Remote_manifest_rejects_an_unsafe_next_check_interval(int? seconds)
    {
        var manifest = new ApplicationUpdateService.UpdateManifest(
            LatestVersion: "1.0.0",
            MinimumSupportedVersion: "1.0.0",
            Message: null,
            SchemaVersion: 2,
            NextCheckAfterSeconds: seconds);

        var valid = ApplicationUpdateService.TryGetNextCheckInterval(
            manifest,
            out _);

        Assert.False(valid);
    }

    [Fact]
    public void TryEvaluate_blocks_an_installation_below_the_minimum_supported_version()
    {
        var manifest = new ApplicationUpdateService.UpdateManifest(
            "1.1.0",
            "1.0.2",
            "Install this security update.",
            SchemaVersion: 2);

        var valid = ApplicationUpdateService.TryEvaluate(
            manifest,
            new Version(1, 0, 0),
            out var status);

        Assert.True(valid);
        Assert.True(status.IsUpdateAvailable);
        Assert.True(status.IsUpdateRequired);
        Assert.Equal("1.0.0", status.InstalledVersion);
        Assert.Equal("1.1.0", status.LatestVersion);
        Assert.Equal("1.0.2", status.MinimumSupportedVersion);
        Assert.Equal(
            new Uri("https://github.com/gpuai404/GarantieExpress/releases/latest"),
            status.DownloadUri);
    }

    [Fact]
    public void TryEvaluate_keeps_an_update_optional_for_a_supported_installation()
    {
        var manifest = new ApplicationUpdateService.UpdateManifest(
            "2.0.0",
            "1.8.0",
            Message: null,
            SchemaVersion: 2);

        var valid = ApplicationUpdateService.TryEvaluate(
            manifest,
            new Version(1, 9, 0),
            out var status);

        Assert.True(valid);
        Assert.True(status.IsUpdateAvailable);
        Assert.False(status.IsUpdateRequired);
    }

    [Fact]
    public void TryEvaluate_requires_a_cross_major_update_below_the_supported_floor()
    {
        var manifest = new ApplicationUpdateService.UpdateManifest(
            "2.1.0",
            "2.0.0",
            Message: null,
            SchemaVersion: 2);

        var valid = ApplicationUpdateService.TryEvaluate(
            manifest,
            new Version(1, 9, 0),
            out var status);

        Assert.True(valid);
        Assert.True(status.IsUpdateRequired);
    }

    [Fact]
    public void TryEvaluate_keeps_an_older_required_floor_when_a_later_release_is_optional()
    {
        var manifest = new ApplicationUpdateService.UpdateManifest(
            "1.4.0",
            "1.2.3",
            Message: null,
            SchemaVersion: 2);

        var valid = ApplicationUpdateService.TryEvaluate(
            manifest,
            new Version(1, 2, 0),
            out var status);

        Assert.True(valid);
        Assert.True(status.IsUpdateAvailable);
        Assert.True(status.IsUpdateRequired);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void TryEvaluate_rejects_an_unsupported_manifest_schema(int schemaVersion)
    {
        var manifest = new ApplicationUpdateService.UpdateManifest(
            "1.1.0",
            "1.0.0",
            Message: null,
            SchemaVersion: schemaVersion);

        var valid = ApplicationUpdateService.TryEvaluate(
            manifest,
            new Version(1, 0, 0),
            out var status);

        Assert.False(valid);
        Assert.False(status.IsUpdateAvailable);
    }

    [Fact]
    public void TryEvaluate_does_not_offer_an_installed_release()
    {
        var manifest = new ApplicationUpdateService.UpdateManifest(
            "1.0.0",
            "1.0.0",
            Message: null,
            SchemaVersion: 2);

        var valid = ApplicationUpdateService.TryEvaluate(
            manifest,
            new Version(1, 0, 0),
            out var status);

        Assert.True(valid);
        Assert.False(status.IsUpdateAvailable);
        Assert.False(status.IsUpdateRequired);
    }

    [Theory]
    [InlineData("1.1")]
    [InlineData("01.1.0")]
    [InlineData("1.1.0-beta")]
    [InlineData("")]
    public void TryEvaluate_rejects_an_invalid_release_version(string latestVersion)
    {
        var manifest = new ApplicationUpdateService.UpdateManifest(
            latestVersion,
            "1.0.0",
            Message: null,
            SchemaVersion: 2);

        var valid = ApplicationUpdateService.TryEvaluate(
            manifest,
            new Version(1, 0, 0),
            out var status);

        Assert.False(valid);
        Assert.False(status.IsUpdateAvailable);
    }

    [Theory]
    [InlineData("1.1")]
    [InlineData("01.1.0")]
    [InlineData("1.1.0-beta")]
    [InlineData("")]
    [InlineData(null)]
    public void TryEvaluate_rejects_an_invalid_minimum_supported_version(
        string? minimumSupportedVersion)
    {
        var manifest = new ApplicationUpdateService.UpdateManifest(
            "1.1.0",
            minimumSupportedVersion,
            Message: null,
            SchemaVersion: 2);

        var valid = ApplicationUpdateService.TryEvaluate(
            manifest,
            new Version(1, 0, 0),
            out var status);

        Assert.False(valid);
        Assert.False(status.IsUpdateAvailable);
    }

    [Fact]
    public void TryEvaluate_rejects_a_minimum_supported_version_above_latest()
    {
        var manifest = new ApplicationUpdateService.UpdateManifest(
            "1.1.0",
            "1.2.0",
            Message: null,
            SchemaVersion: 2);

        var valid = ApplicationUpdateService.TryEvaluate(
            manifest,
            new Version(1, 0, 0),
            out var status);

        Assert.False(valid);
        Assert.False(status.IsUpdateAvailable);
    }

    private sealed class FixedHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class ManifestHandler(string manifest) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(manifest, Encoding.UTF8, "application/json")
            });
    }
}

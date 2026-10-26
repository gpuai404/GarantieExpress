using GarantieExpress.Host.Updates;
using Xunit;

namespace GarantieExpress.Host.Tests;

public sealed class ApplicationUpdateServiceTests
{
    [Fact]
    public void Periodic_checks_run_every_eight_hours()
    {
        Assert.Equal(
            TimeSpan.FromHours(8),
            ApplicationUpdateMonitoringService.CheckInterval);
    }

    [Fact]
    public void TryEvaluate_blocks_an_installation_below_the_minimum_supported_version()
    {
        var manifest = new ApplicationUpdateService.UpdateManifest(
            "1.1.0",
            "1.0.1",
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
        Assert.Equal("1.0.1", status.MinimumSupportedVersion);
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
}

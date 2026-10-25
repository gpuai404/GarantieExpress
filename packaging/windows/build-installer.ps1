[CmdletBinding()]
param(
    [ValidateSet("x64", "arm64")]
    [string] $Architecture = "x64",

    [Parameter(Mandatory = $true)]
    [ValidatePattern("^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$")]
    [string] $Version,

    [switch] $Unsigned,

    [string] $CertificateThumbprint = $env:WINDOWS_SIGNING_CERTIFICATE_THUMBPRINT,

    [string] $TimestampUrl = "http://timestamp.digicert.com"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Set-WindowsGuiSubsystem {
    param([Parameter(Mandatory)][string] $ExecutablePath)

    [byte[]] $image = [IO.File]::ReadAllBytes($ExecutablePath)
    if ($image.Length -lt 128 -or $image[0] -ne 0x4d -or $image[1] -ne 0x5a) {
        throw "$ExecutablePath is not a valid Windows executable."
    }

    $peOffset = [BitConverter]::ToInt32($image, 0x3c)
    if ($peOffset -lt 0 -or $peOffset + 94 -gt $image.Length `
        -or $image[$peOffset] -ne 0x50 `
        -or $image[$peOffset + 1] -ne 0x45 `
        -or $image[$peOffset + 2] -ne 0 `
        -or $image[$peOffset + 3] -ne 0) {
        throw "$ExecutablePath has an invalid PE header."
    }

    $optionalHeaderOffset = $peOffset + 24
    $optionalHeaderMagic = [BitConverter]::ToUInt16($image, $optionalHeaderOffset)
    if ($optionalHeaderMagic -ne 0x10b -and $optionalHeaderMagic -ne 0x20b) {
        throw "$ExecutablePath has an unsupported PE optional header."
    }

    # IMAGE_SUBSYSTEM_WINDOWS_GUI prevents a console window when launched by the shortcut.
    [BitConverter]::GetBytes([uint16] 2).CopyTo($image, $optionalHeaderOffset + 68)
    [Array]::Clear($image, $optionalHeaderOffset + 64, 4)
    [IO.File]::WriteAllBytes($ExecutablePath, $image)
}

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$runtimeIdentifier = "win-$Architecture"
$hostProject = Join-Path $repoRoot "src\GarantieExpress.Host\GarantieExpress.Host.csproj"
$installerProject = Join-Path $PSScriptRoot "GarantieExpress.Installer.wixproj"
$artifactRoot = Join-Path $repoRoot "artifacts\package\windows\$runtimeIdentifier"
$publishDirectory = Join-Path $artifactRoot "publish"
$installerDirectory = Join-Path $artifactRoot "installer"

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($null -eq $dotnet) {
    throw "The .NET SDK is required to create the installer."
}

$projectVersion = (& $dotnet.Source msbuild $hostProject -getProperty:VersionPrefix).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Could not read VersionPrefix from the host project."
}
if ($Version -ne $projectVersion) {
    throw "Package version $Version must match the application version $projectVersion."
}

$signTool = $null
if (-not $Unsigned -and [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    throw "Set WINDOWS_SIGNING_CERTIFICATE_THUMBPRINT or pass -CertificateThumbprint. Use -Unsigned only for local package validation."
}
if (-not $Unsigned) {
    $signTool = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($null -eq $signTool) {
        throw "signtool.exe is required for a signed release package."
    }
}

if (Test-Path -LiteralPath $artifactRoot) {
    Remove-Item -LiteralPath $artifactRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDirectory, $installerDirectory | Out-Null

& $dotnet.Source publish $hostProject `
    --configuration Release `
    --runtime $runtimeIdentifier `
    --self-contained true `
    --output $publishDirectory `
    -p:PublishProfile=$runtimeIdentifier `
    -p:Version=$Version
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$applicationExecutable = Join-Path $publishDirectory "GarantieExpress.exe"
Set-WindowsGuiSubsystem -ExecutablePath $applicationExecutable

if (-not $Unsigned) {
    & $signTool.Source sign `
        /sha1 $CertificateThumbprint `
        /fd SHA256 `
        /tr $TimestampUrl `
        /td SHA256 `
        $applicationExecutable
    if ($LASTEXITCODE -ne 0) {
        throw "Signing GarantieExpress.exe failed with exit code $LASTEXITCODE."
    }
}

& $dotnet.Source build $installerProject `
    --configuration Release `
    -p:PackageVersion=$Version `
    -p:InstallerPlatform=$Architecture `
    -p:PublishDirectory=$publishDirectory `
    -p:OutputPath=$installerDirectory
if ($LASTEXITCODE -ne 0) {
    throw "Building the MSI failed with exit code $LASTEXITCODE."
}

$msi = Get-ChildItem -LiteralPath $installerDirectory -Filter "*.msi" -Recurse |
    Select-Object -First 1
if ($null -eq $msi) {
    throw "The MSI build completed without producing an installer."
}

if (-not $Unsigned) {
    & $signTool.Source sign `
        /sha1 $CertificateThumbprint `
        /fd SHA256 `
        /tr $TimestampUrl `
        /td SHA256 `
        $msi.FullName
    if ($LASTEXITCODE -ne 0) {
        throw "Signing the MSI failed with exit code $LASTEXITCODE."
    }

    & $signTool.Source verify /pa /all $msi.FullName
    if ($LASTEXITCODE -ne 0) {
        throw "MSI signature verification failed with exit code $LASTEXITCODE."
    }
}

$checksumPath = "$($msi.FullName).sha256"
$checksum = (Get-FileHash -LiteralPath $msi.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(
    $checksumPath,
    "$checksum *$($msi.Name)`n",
    [Text.Encoding]::ASCII)

Write-Output $msi.FullName
Write-Output $checksumPath

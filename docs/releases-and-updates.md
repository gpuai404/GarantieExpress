# Releases and application updates

GarantieExpress uses stable semantic versions in `MAJOR.MINOR.PATCH` format.
The installed version is defined by `VersionPrefix` in
[`Directory.Build.props`](../Directory.Build.props).

## Publishing a release

Update `VersionPrefix` and `.github/pages/update.json` in the same commit, then
push the commit and wait for CI to pass. Create and push a matching `vX.Y.Z`
tag only after the branch build succeeds. For version `1.0.1`:

```bash
git tag v1.0.1
git push origin v1.0.1
```

The tag build validates that the tag, application version, and update manifest
match. It builds Windows MSI and macOS PKG installers for x64 and Arm64, creates
SHA-256 checksum files, and publishes all eight files on the corresponding
GitHub Release.

The CI packages are currently unsigned. Windows SmartScreen and macOS
Gatekeeper can warn or block users until code-signing certificates and Apple
notarization credentials are configured in the release workflow.

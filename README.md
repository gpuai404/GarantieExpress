# GarantieExpress

GarantieExpress is a local-first desktop application for managing product
warranties and printing warranty certificates. It is built with .NET 10,
Blazor Server, and SQLite.

## Features

- Create and manage warranty certificates.
- Keep a local history of issued warranties and product presets.
- Configure warranty categories, shop details, and certificate appearance.
- Store application data locally in SQLite.

## Requirements

- .NET SDK 10.0.100 or a compatible .NET 10 feature-band release.
- Node.js 22 or later to run the JavaScript and browser tests.
- Playwright's Chromium browser to run the end-to-end tests.

## Run locally

```sh
dotnet restore GarantieExpress.slnx
dotnet run --project src/GarantieExpress.Host/GarantieExpress.Host.csproj
```

The development host listens on `http://127.0.0.1:5248`. It binds to loopback
only and is not intended to be exposed to a network.

By default, the SQLite database is stored in the current user's application
data directory:

- Windows: `%LOCALAPPDATA%\GarantieExpress`
- macOS: `~/Library/Application Support/GarantieExpress`
- Other platforms: `~/.local/share/GarantieExpress`

The database contains user-managed warranty and shop data. Back it up using
your operating system's normal file-backup tools while the application is
closed.

## Updates

The application checks a static GitHub Pages manifest when it starts. Newer
supported releases show an update notice; installations older than the
manifest's minimum supported version show a required-update screen. See
[`docs/releases-and-updates.md`](./docs/releases-and-updates.md) for the
manifest contract and publishing process.

## Test

```sh
dotnet build GarantieExpress.slnx
dotnet format GarantieExpress.slnx --verify-no-changes --no-restore
dotnet test GarantieExpress.slnx --no-build
npm ci
npm test
npx playwright install chromium
npm run test:e2e
```

## Security

GarantieExpress is designed to run locally and stores application data on the
device. See [SECURITY.md](./SECURITY.md) for security-reporting guidance.

## License

This repository is public for source inspection. It is **not open source** and
does not grant permission to use, modify, or redistribute the software. See
[`LICENSE`](./LICENSE).

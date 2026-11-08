# HN Reader

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![Platform](https://img.shields.io/badge/platform-Windows-0078D4)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![Avalonia](https://img.shields.io/badge/Avalonia-11-blue)

A modern, fast Hacker News reader for Windows.


## Features

- **Browse Hacker News** — Top, New, Best, Ask HN, and Show HN feeds with paginated infinite scroll
- **Comment sorting** — Re-sort loaded comment threads by Top, New, or most-replied
- **Favorites** — Save stories locally with LiteDB; export/import as JSON for backup
- **Filter favourites** — Search your saved stories by title, author, or domain
- **Modern UI** — Avalonia with a collapsible navigation pane, light/dark theme, and an extended title bar

## Download

Get the latest Windows build from the [**Releases page**](https://github.com/ZadokJoshua/hn-reader/releases/latest):
download `HNReader-<version>-win-x64.zip`, extract it, and run `HNReader.exe` (keep `Fizzler.dll` beside it).
Windows 10/11 64-bit; .NET is included, nothing else to install.

The build isn't code-signed yet, so Windows SmartScreen may show "Windows protected your PC" on first launch —
choose **More info → Run anyway**. Each release includes a `.sha256` checksum, and every download is built by the
public [release workflow](.github/workflows/release.yml), so you can also build it yourself.

## Build from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (the test project also needs .NET 10).

```powershell
git clone https://github.com/ZadokJoshua/hn-reader.git
cd hn-reader

# Run it
dotnet run --project src/HNReader.Avalonia

# Run the tests
dotnet test

# Produce the same self-contained Windows build as a release
dotnet publish src/HNReader.Avalonia -c Release -r win-x64 -o publish
```

## License

[MIT](LICENSE). Bundled third-party components are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

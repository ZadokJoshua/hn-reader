## Install

1. Download **`HNReader-<version>-win-x64.zip`** below.
2. Extract it to a folder of your choice (for example `Documents\HN Reader`).
3. Run **`HNReader.exe`**. Keep `Fizzler.dll` in the same folder.

Works on Windows 10 and 11 (64-bit). Nothing else to install — .NET is included.

**"Windows protected your PC"?** The app isn't code-signed yet, so SmartScreen may warn on first launch. Click **More info → Run anyway**. You can check the download against the `.sha256` file, or build it yourself from the source below.

## Good to know

- The app talks only to Hacker News, plus favicon lookups for story sites. No accounts, no tracking.
- Your favourites and settings are stored in `%APPDATA%\HNReader`.
- To uninstall, delete the folder you extracted, and `%APPDATA%\HNReader` if you also want to remove your data.

## Open source

HN Reader is MIT-licensed. This build was produced by the public [release workflow](https://github.com/ZadokJoshua/hn-reader/blob/main/.github/workflows/release.yml) from this tag; the source code is attached below. Bundled third-party components and their licenses are listed in `THIRD-PARTY-NOTICES.md` inside the zip.

Found a bug or have an idea? [Open an issue](https://github.com/ZadokJoshua/hn-reader/issues).

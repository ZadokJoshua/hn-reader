# HN Reader

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![Platform](https://img.shields.io/badge/platform-Windows-0078D4)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![Avalonia](https://img.shields.io/badge/Avalonia-11-blue)

A modern, fast Hacker News reader for Windows. No telemetry - just a clean Avalonia client for the stories you care about.


## Features

- **Browse Hacker News** — Top, New, Best, Ask HN, and Show HN feeds with paginated infinite scroll
- **Quick filters** — One-click toggle to show only Ask HN or only Show HN posts on any feed
- **Comment sorting** — Re-sort loaded comment threads by Top, New, or most-replied
- **Full comment threads** — Recursive reply tree parsed from the live HN site (faster than the official API)
- **Favorites** — Save stories locally with LiteDB; export/import as JSON for backup
- **Filter favourites** — Search your saved stories by title, author, or domain
- **Safe links** — Links in comments show their real destination before opening (can be turned off in Settings)
- **Modern UI** — Avalonia with a collapsible navigation pane, light/dark theme, and an extended title bar

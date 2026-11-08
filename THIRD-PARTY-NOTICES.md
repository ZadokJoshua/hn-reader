# Third-party notices

HN Reader is licensed under the MIT License (see `LICENSE`). The Windows download bundles the
components below, each under its own license. Full license texts are available at the linked
projects; licenses were taken from each package's published metadata.

## Permissive licenses (MIT, BSD, MS-PL, OFL)

| Component | License | Project |
|---|---|---|
| .NET runtime and libraries, Microsoft.Extensions.* | MIT | https://github.com/dotnet/runtime |
| Avalonia (core, Desktop, Win32, Skia, Themes.Fluent, Fonts.Inter) | MIT | https://github.com/AvaloniaUI/Avalonia |
| Inter typeface (bundled by Avalonia.Fonts.Inter) | SIL Open Font License 1.1 | https://github.com/rsms/inter |
| ANGLE (Avalonia.Angle.Windows.Natives) | BSD-3-Clause | https://chromium.googlesource.com/angle/angle |
| SkiaSharp, HarfBuzzSharp | MIT | https://github.com/mono/SkiaSharp |
| Skia | BSD-3-Clause | https://skia.org |
| HarfBuzz | MIT ("Old MIT") | https://github.com/harfbuzz/harfbuzz |
| Markdown.Avalonia (Tight, Html, Svg, SyntaxHigh), ColorDocument.Avalonia, ColorTextBlock.Avalonia | MIT | https://github.com/whistyun/Markdown.Avalonia |
| AvaloniaEdit | MIT | https://github.com/AvaloniaUI/AvaloniaEdit |
| Avalonia.Svg, Svg.Model, ShimSkiaSharp | MIT | https://github.com/wieslawsoltes/Svg.Skia |
| Svg.Custom | MS-PL | https://github.com/wieslawsoltes/Svg.Skia |
| ExCSS | MIT | https://github.com/TylerBrinks/ExCSS |
| AsyncImageLoader.Avalonia | MIT | https://github.com/AvaloniaUtils/AsyncImageLoader.Avalonia |
| CommunityToolkit.Mvvm | MIT | https://github.com/CommunityToolkit/dotnet |
| HtmlAgilityPack | MIT | https://github.com/zzzprojects/html-agility-pack |
| LiteDB | MIT | https://github.com/litedb-org/LiteDB |
| MicroCom.Runtime | MIT | https://github.com/kekekeks/MicroCom |
| Tmds.DBus.Protocol | MIT | https://github.com/tmds/Tmds.DBus |
| Phosphor Icons (icon font) | MIT | https://github.com/phosphor-icons/core |

## LGPL-3.0

| Component | License | Project |
|---|---|---|
| Fizzler 1.2.1 (used by Svg.Custom) | GNU LGPL 3.0 | https://github.com/atifaziz/Fizzler |

Fizzler is shipped unmodified as the separate file `Fizzler.dll`, not inside `HNReader.exe`, so it
can be replaced with another compatible build of the library. Its source code is available at the
link above, and the full LGPL-3.0 text at https://www.gnu.org/licenses/lgpl-3.0.html.

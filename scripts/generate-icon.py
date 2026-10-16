"""Generates the app icon from the same design as the in-app HnLogo control.

Run from the repo root:

    python tools/generate-icon.py

Writes src/HNReader.Avalonia/Assets/HnReaderIcon.ico (multi-size) and
HnReaderIconImg.png. The .ico path is referenced by MainWindow.axaml and by
<ApplicationIcon> in HNReader.Avalonia.csproj, so the filename must not change.

The mark is a newspaper page with an orange masthead carrying "HN". It is
deliberately not the Hacker News logo, which is Y Combinator's; the page, the
lines and the two letters are generic shapes.

This exists so the icon is reproducible rather than an opaque binary checked in
once and never explainable again. It mirrors Controls/HnLogo.axaml by hand -
Avalonia XAML cannot be rendered offline - so a change to one wants the same
change in the other.
"""

from __future__ import annotations

import os
import sys

from PIL import Image, ImageDraw, ImageFont

ACCENT = (255, 102, 0, 255)
PAGE = (255, 255, 255, 255)
# Not pure black: the icon is drawn light-on-transparent so it reads on both a
# light and a dark taskbar, and a softer stroke keeps the small sizes from
# turning into a solid block.
STROKE = (60, 64, 72, 255)
LINE = (140, 146, 156, 255)

# Everything is authored on this grid and scaled, matching HnLogo.axaml's 64x64
# design canvas, so the proportions stay identical between the two.
GRID = 64

ICO_SIZES = [16, 24, 32, 48, 64, 128, 256]

# Below this the wordmark would be a handful of pixels tall, so the simplified
# variant drops it and shows the masthead as a solid block instead.
DETAIL_THRESHOLD = 32


def _font(px: int) -> ImageFont.FreeTypeFont | None:
    for path in (r"C:\Windows\Fonts\segoeuib.ttf", r"C:\Windows\Fonts\arialbd.ttf"):
        if os.path.exists(path):
            try:
                return ImageFont.truetype(path, px)
            except OSError:
                continue
    return None


def render(size: int) -> Image.Image:
    # Drawn at 8x and downsampled: PIL has no antialiased vector rasteriser, so
    # supersampling is what keeps the rounded corners and thin lines clean.
    scale = 8
    px = size * scale
    img = Image.new("RGBA", (px, px), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    def u(v: float) -> float:
        """Design-grid units to pixels."""
        return v * px / GRID

    detailed = size >= DETAIL_THRESHOLD

    # The page
    d.rounded_rectangle(
        [u(9), u(6), u(55), u(58)],
        radius=u(5),
        fill=PAGE,
        outline=STROKE,
        width=max(1, int(u(2.5))),
    )

    # Masthead
    d.rounded_rectangle(
        [u(15), u(12), u(49), u(25)],
        radius=u(2.5),
        fill=ACCENT,
    )

    if detailed:
        font = _font(int(u(9.5)))
        if font is not None:
            d.text(
                (u(32), u(18.5)),
                "HN",
                font=font,
                fill=PAGE,
                anchor="mm",
            )

    # Body lines. The simplified variant keeps only the two widest, so the small
    # sizes read as a page rather than a grey smear.
    lines = [(31, 34), (38, 28), (45, 32), (52, 20)]
    if not detailed:
        lines = [(33, 34), (43, 26)]

    for top, width in lines:
        height = 4 if detailed else 5
        d.rounded_rectangle(
            [u(15), u(top), u(15 + width), u(top + height)],
            radius=u(height / 2),
            fill=LINE,
        )

    return img.resize((size, size), Image.LANCZOS)


def main() -> int:
    here = os.path.dirname(os.path.abspath(__file__))
    assets = os.path.join(here, "..", "src", "HNReader.Avalonia", "Assets")
    assets = os.path.normpath(assets)

    if not os.path.isdir(assets):
        print(f"assets folder not found: {assets}", file=sys.stderr)
        return 1

    frames = [render(s) for s in ICO_SIZES]

    ico = os.path.join(assets, "HnReaderIcon.ico")
    # Pillow writes every requested size into the one file; passing the frames as
    # append_images keeps the hand-tuned small variants instead of letting it
    # downscale the 256 for all of them.
    frames[-1].save(
        ico,
        format="ICO",
        sizes=[(s, s) for s in ICO_SIZES],
        append_images=frames[:-1],
    )

    png = os.path.join(assets, "HnReaderIconImg.png")
    render(256).save(png, format="PNG")

    print(f"wrote {ico}")
    print(f"wrote {png}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

"""Regenerate HNReader WinUI app package assets from HnReaderIconImg.png.

Source: src/HNReader.WinUI/Assets/HnReaderIconImg.png  (250x198 RGBA, "Hn" mark)

Outputs (all overwritten in place):
  Assets/StoreLogo.png                                       50x50
  Assets/LockScreenLogo.scale-200.png                         48x48
  Assets/Square44x44Logo.scale-200.png                       88x88
  Assets/Square44x44Logo.targetsize-24_altform-unplated.png  24x24
  Assets/Square150x150Logo.scale-200.png                     300x300
  Assets/Wide310x150Logo.scale-200.png                      620x300
  Assets/SplashScreen.scale-200.png                         1240x600
  HnReaderIcon.ico                                           256/64/48/32/16
"""
from __future__ import annotations

import os
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
ASSETS = os.path.join(ROOT, "src", "HNReader.WinUI", "Assets")
ICON_PATH = os.path.join(ROOT, "src", "HNReader.WinUI", "HnReaderIcon.ico")
SOURCE = os.path.join(ASSETS, "HnReaderIconImg.png")

# Dark/orange gradient palette sampled from the source image style.
GRAD_TOP = (26, 18, 22)        # near-black warm
GRAD_BOTTOM = (88, 28, 14)     # deep orange-brown
GRAD_HIGHLIGHT = (255, 122, 36)  # brand orange highlight (vertical band)

HIGH_QUALITY = Image.Resampling.LANCZOS


def load_source() -> Image.Image:
    img = Image.open(SOURCE).convert("RGBA")
    return img


def make_square(size: int) -> Image.Image:
    """Resize the source to a square of `size`x`size` directly (no canvas)."""
    return load_source().resize((size, size), HIGH_QUALITY)


def make_square_sharp(size: int) -> Image.Image:
    """Square resize with mild sharpen to keep small icons legible."""
    img = make_square(size)
    return img.filter(ImageFilter.UnsharpMask(radius=1, percent=140, threshold=2))


def make_gradient_canvas(w: int, h: int) -> Image.Image:
    """Vertical dark->orange-brown gradient with a soft orange highlight band."""
    base = Image.new("RGB", (w, h), GRAD_TOP)
    top = Image.new("RGB", (w, h), GRAD_BOTTOM)
    mask = Image.new("L", (w, h))
    for y in range(h):
        # ease-in-out style vertical gradient
        t = y / max(1, h - 1)
        mask.paste(int(255 * t), (0, y, w, y + 1))
    base = Image.composite(top, base, mask)

    # Soft vertical highlight band
    overlay = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    band_w = int(w * 0.22)
    cx = w // 2
    for x in range(cx - band_w // 2, cx + band_w // 2):
        dx = abs(x - cx) / (band_w / 2)
        a = int(60 * max(0.0, 1 - dx) ** 2)
        draw.line([(x, 0), (x, h)], fill=(255, 122, 36, a))
    overlay = overlay.filter(ImageFilter.GaussianBlur(radius=min(w, h) * 0.02))
    base = Image.alpha_composite(base.convert("RGBA"), overlay).convert("RGB")

    # Subtle vignette
    vignette = Image.new("L", (w, h), 0)
    vdraw = ImageDraw.Draw(vignette)
    vdraw.ellipse(
        (-int(w * 0.25), -int(h * 0.4), int(w * 1.25), int(h * 1.4)),
        fill=255,
    )
    vignette = vignette.filter(ImageFilter.GaussianBlur(radius=min(w, h) * 0.25))
    black = Image.new("RGB", (w, h), (0, 0, 0))
    base = Image.composite(base, black, vignette)
    return base


def paste_centered(canvas: Image.Image, fg: Image.Image) -> Image.Image:
    out = canvas.convert("RGBA")
    x = (out.width - fg.width) // 2
    y = (out.height - fg.height) // 2
    out.alpha_composite(fg, (x, y))
    return out.convert("RGB")


def make_wide(w: int, h: int, icon_max_h_ratio: float = 0.75) -> Image.Image:
    canvas = make_gradient_canvas(w, h)
    src = load_source()
    # Fit icon to ~75% of canvas height while preserving aspect.
    target_h = int(h * icon_max_h_ratio)
    scale = target_h / src.height
    target_w = int(src.width * scale)
    icon = src.resize((target_w, target_h), HIGH_QUALITY)
    return paste_centered(canvas, icon)


def write_square_assets() -> None:
    make_square(50).save(os.path.join(ASSETS, "StoreLogo.png"))
    make_square_sharp(48).save(os.path.join(ASSETS, "LockScreenLogo.scale-200.png"))
    make_square_sharp(88).save(os.path.join(ASSETS, "Square44x44Logo.scale-200.png"))
    make_square_sharp(24).save(
        os.path.join(ASSETS, "Square44x44Logo.targetsize-24_altform-unplated.png")
    )
    make_square(300).save(os.path.join(ASSETS, "Square150x150Logo.scale-200.png"))


def write_wide_assets() -> None:
    wide = make_wide(620, 300, icon_max_h_ratio=0.78)
    wide.save(os.path.join(ASSETS, "Wide310x150Logo.scale-200.png"))
    splash = make_wide(1240, 600, icon_max_h_ratio=0.72)
    splash.save(os.path.join(ASSETS, "SplashScreen.scale-200.png"))


def write_ico() -> None:
    sizes = [(256, 256), (64, 64), (48, 48), (32, 32), (16, 16)]
    frames = []
    for size in sizes:
        if size[0] <= 48:
            frames.append(make_square_sharp(size[0]))
        else:
            frames.append(make_square(size[0]))
    base = frames[0]
    base.save(
        ICON_PATH,
        format="ICO",
        sizes=[f.size for f in frames],
        append_images=frames[1:],
    )


def write_contact_sheet(out_path: str) -> None:
    """Build a single PNG laying out every generated asset for visual review."""
    rows = []

    # Row 1: square assets shown at their native pixel size on a checkered
    # background so transparent edges are visible.
    square_assets = [
        ("StoreLogo 50", os.path.join(ASSETS, "StoreLogo.png"), 50),
        ("LockScreenLogo 48", os.path.join(ASSETS, "LockScreenLogo.scale-200.png"), 48),
        ("Square44x44 88", os.path.join(ASSETS, "Square44x44Logo.scale-200.png"), 88),
        ("Square44x44 24", os.path.join(ASSETS, "Square44x44Logo.targetsize-24_altform-unplated.png"), 24),
        ("Square150x150 300", os.path.join(ASSETS, "Square150x150Logo.scale-200.png"), 300),
    ]
    rows.append(("Square assets (native size on checker)", square_assets))

    # Row 2: wide + splash
    wide_assets = [
        ("Wide310x150 620x300", os.path.join(ASSETS, "Wide310x150Logo.scale-200.png"), None),
        ("SplashScreen 1240x600", os.path.join(ASSETS, "SplashScreen.scale-200.png"), None),
    ]
    rows.append(("Wide / Splash (scaled to fit)", wide_assets))

    # Row 3: ICO frames at native size on checker
    ico_assets = [
        (f"ICO {s}x{s}", ICON_PATH, s) for s in (256, 64, 48, 32, 16)
    ]
    rows.append(("HnReaderIcon.ico frames (native size on checker)", ico_assets))

    label_h = 40
    pad = 24
    checker_cell = 12
    max_row_w = 0
    row_imgs = []
    for title, items in rows:
        cells = []
        for name, path, native in items:
            img = Image.open(path).convert("RGBA")
            if native is not None and img.size[0] != native:
                # ICO entries: extract the requested frame.
                img = _extract_ico_frame(path, native)
            cells.append((name, img))
        row_img, row_w = _compose_row(cells, pad, checker_cell)
        # Title strip
        title_img = Image.new("RGB", (row_w, label_h), (24, 24, 28))
        d = ImageDraw.Draw(title_img)
        d.text((pad, 10), title, fill=(240, 240, 240))
        row_imgs.append(Image.new("RGB", (row_w, 0)))
        row_imgs[-1] = Image.new("RGB", (row_w, label_h + row_img.height), (24, 24, 28))
        row_imgs[-1].paste(title_img, (0, 0))
        row_imgs[-1].paste(row_img, (0, label_h))
        max_row_w = max(max_row_w, row_w)

    sheet_h = sum(r.height for r in row_imgs) + pad * (len(row_imgs) + 1)
    sheet = Image.new("RGB", (max_row_w + pad * 2, sheet_h), (18, 18, 22))
    y = pad
    for r in row_imgs:
        sheet.paste(r, (pad, y))
        y += r.height + pad
    sheet.save(out_path)
    print("Wrote contact sheet:", os.path.relpath(out_path, ROOT))


def _extract_ico_frame(ico_path: str, size: int) -> Image.Image:
    """Return the ICO frame that matches `size`x`size`, or the largest frame."""
    img = Image.open(ico_path)
    sizes = []
    try:
        for s in img.ico.sizes():
            sizes.append(s)
    except Exception:
        sizes = [img.size]
    if (size, size) in sizes:
        img.size = (size, size)
    return img.convert("RGBA")


def _checker(w: int, h: int, cell: int) -> Image.Image:
    img = Image.new("RGB", (w, h), (60, 60, 66))
    d = ImageDraw.Draw(img)
    for y in range(0, h, cell):
        for x in range(0, w, cell):
            if ((x // cell) + (y // cell)) % 2 == 0:
                d.rectangle([x, y, x + cell, y + cell], fill=(90, 90, 96))
    return img


def _compose_row(cells, pad: int, checker_cell: int):
    cell_w = max(c[1].size[0] for c in cells) + pad * 2
    cell_h = max(c[1].size[1] for c in cells) + pad * 2 + 28
    row_w = cell_w * len(cells)
    row = _checker(row_w, cell_h, checker_cell)
    for i, (name, img) in enumerate(cells):
        x = i * cell_w + (cell_w - img.size[0]) // 2
        y = (cell_h - 28 - img.size[1]) // 2
        row.paste(img, (x, y), img)
        d = ImageDraw.Draw(row)
        d.text((i * cell_w + pad, cell_h - 24), name, fill=(230, 230, 230))
    return row, row_w


def main() -> None:
    assert os.path.isfile(SOURCE), f"Missing source: {SOURCE}"
    os.makedirs(ASSETS, exist_ok=True)
    write_square_assets()
    write_wide_assets()
    write_ico()
    sheet_path = os.path.join(ROOT, "scripts", "app-package-assets-contact-sheet.png")
    write_contact_sheet(sheet_path)
    print("Regenerated assets from", os.path.relpath(SOURCE, ROOT))


if __name__ == "__main__":
    main()

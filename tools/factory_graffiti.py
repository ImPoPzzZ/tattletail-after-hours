"""Night Shift at Waygetter: spray-paint graffiti textures (transparent PNGs the DLL embeds as
assets/factory/graf_<key>.png). Text is painted like a spray can: soft edges, overspray mist and
speckles, drips running down from the letters, sometimes an outline (throw-ups).

  python tools/factory_graffiti.py
"""
import os
import random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "assets", "factory")
FONTS = r"C:\Windows\Fonts"
THIN = ("Inkfree.ttf", "segoeprb.ttf")

# key: (text, font, size px, fill RGB, outline RGB or None, drips 0..1)
PIECES = {
    "knew":  ("WAYGETTER KNEW", "Inkfree.ttf", 150, (150, 18, 16), None, 0.8),
    "rip":   ("RIP 1998", "segoeprb.ttf", 120, (215, 212, 200), None, 0.25),
    "still": ("SHE'S STILL HERE", "Inkfree.ttf", 140, (160, 16, 14), None, 1.0),
    "sees":  ("MAMA SEES YOU", "comicbd.ttf", 150, (125, 60, 160), (15, 12, 18), 0.5),
    "home":  ("GO HOME", "impact.ttf", 170, (225, 222, 210), None, 0.7),
    "dont":  ("DON'T OPEN IT", "Inkfree.ttf", 130, (175, 20, 16), None, 0.9),
    "kz":    ("KZ '09", "comicbd.ttf", 190, (60, 150, 80), (12, 14, 12), 0.3),
    "was":   ("2011 WAS HERE", "segoeprb.ttf", 110, (70, 105, 190), None, 0.2),
    "lies":  ("TATTLETAIL LIES", "impact.ttf", 150, (230, 190, 40), (20, 18, 12), 0.6),
    "count": ("WHO'S COUNTING?", "Inkfree.ttf", 110, (205, 200, 185), None, 0.4),
}


def spray(text, font_file, size, fill, outline, drips, seed):
    rnd = random.Random(seed)
    np_rnd = np.random.RandomState(seed)
    font = ImageFont.truetype(os.path.join(FONTS, font_file), size)
    stroke = max(4, size // 14) if outline else 0
    x0, y0, x1, y1 = font.getbbox(text, stroke_width=stroke)
    pad = size // 3
    drip_room = int(size * 1.1 * drips) + pad
    w, h = x1 - x0 + pad * 2, y1 - y0 + pad + drip_room
    ox, oy = pad - x0, pad - y0

    def text_mask(stroke_w):
        m = Image.new("L", (w, h), 0)
        ImageDraw.Draw(m).text((ox, oy), text, font=font, fill=255, stroke_width=stroke_w, stroke_fill=255)
        if font_file in THIN:  # handwriting fonts are pen-thin: fatten them to a spray can's line
            k = max(3, size // 22) | 1
            m = m.filter(ImageFilter.MaxFilter(k))
        return m

    fill_mask = np.asarray(text_mask(0), dtype=np.float32) / 255.0
    out_mask = np.asarray(text_mask(stroke), dtype=np.float32) / 255.0 if outline else None
    base = out_mask if outline else fill_mask

    # Drips: from the bottom edge of the letters, straight down, thinning, with a bead at the end.
    drip_img = Image.new("L", (w, h), 0)
    dd = ImageDraw.Draw(drip_img)
    cols = [x for x in range(0, w, 3) if base[:, x].max() > 0.5]
    for x in rnd.sample(cols, int(len(cols) * 0.06 * drips)) if cols else []:
        ys = np.where(base[:, x] > 0.5)[0]
        y = int(ys.max())
        length = int(size * rnd.uniform(0.15, 1.0) * drips)
        width = rnd.uniform(1.5, 4.5) * size / 120
        for k in range(length):
            a = int(255 * (1 - k / max(1, length) * 0.5))
            r = max(0.8, width * (1 - k / max(1, length) * 0.6))
            dd.ellipse([x - r, y + k - r, x + r, y + k + r], fill=a)
        r = width * 1.4
        dd.ellipse([x - r, y + length - r, x + r, y + length + r], fill=200)
    drip = np.asarray(drip_img, dtype=np.float32) / 255.0

    # The spray: a soft core, a wide faint mist, and speckles flecked around it.
    def soft(m, r):
        return np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(r)), dtype=np.float32) / 255.0
    edge = size / 120.0
    core_fill = np.clip(soft(np.maximum(fill_mask, drip * (0 if outline else 1)), 1.1 * edge) * 1.15, 0, 1)
    mist = soft(base, 9 * edge) * 0.28
    speck_zone = soft(base, 14 * edge)
    speck = (np_rnd.rand(h, w) < speck_zone * 0.08).astype(np.float32) * np_rnd.uniform(0.3, 0.8, (h, w))
    grain = 0.82 + 0.18 * np_rnd.rand(h, w)                     # uneven paint

    rgba = np.zeros((h, w, 4), dtype=np.float32)
    if outline:
        core_out = np.clip(soft(np.maximum(out_mask, drip), 1.1 * edge) * 1.15, 0, 1)
        a_out = np.clip(np.maximum(np.maximum(core_out, mist), speck) * grain, 0, 1)
        rgba[..., :3] = np.array(outline, dtype=np.float32) / 255.0
        rgba[..., 3] = a_out
        # the fill sprayed over the outline
        a_fill = core_fill * grain
        f = np.array(fill, dtype=np.float32) / 255.0
        rgba[..., :3] = rgba[..., :3] * (1 - a_fill[..., None]) + f * a_fill[..., None]
        rgba[..., 3] = np.maximum(rgba[..., 3], a_fill)
    else:
        rgba[..., :3] = np.array(fill, dtype=np.float32) / 255.0
        rgba[..., 3] = np.clip(np.maximum(np.maximum(core_fill, mist), speck) * grain, 0, 1)
    img = Image.fromarray((np.clip(rgba, 0, 1) * 255).astype(np.uint8), "RGBA")
    # Keep it a sane size for the game (and a power-of-two-friendly width).
    scale = min(1.0, 1024.0 / img.width)
    if scale < 1.0:
        img = img.resize((int(img.width * scale), int(img.height * scale)), Image.LANCZOS)
    return img


def eye(seed):
    """A spray-painted eye (by the R&D door)."""
    size = 360
    m = Image.new("L", (size, size), 0)
    d = ImageDraw.Draw(m)
    d.ellipse([30, 110, 330, 250], outline=255, width=16)
    d.ellipse([140, 130, 220, 230], fill=255)
    for i in range(7):
        x = 60 + i * 42
        d.line([x, 105, x - 10 + i * 3, 60], fill=255, width=9)
    base = np.asarray(m, dtype=np.float32) / 255.0
    soft = lambda a, r: np.asarray(Image.fromarray((a * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(r)), dtype=np.float32) / 255.0
    rs = np.random.RandomState(seed)
    alpha = np.clip(np.maximum(soft(base, 2) * 1.2, soft(base, 12) * 0.3) * (0.82 + 0.18 * rs.rand(size, size)), 0, 1)
    rgba = np.zeros((size, size, 4), dtype=np.float32)
    rgba[..., :3] = np.array((170, 18, 14)) / 255.0
    rgba[..., 3] = alpha
    return Image.fromarray((rgba * 255).astype(np.uint8), "RGBA")


def main():
    os.makedirs(OUT, exist_ok=True)
    total = 0
    for i, (key, (text, font, size, fill, outline, drips)) in enumerate(PIECES.items()):
        img = spray(text, font, size, fill, outline, drips, 1998 + i)
        path = os.path.join(OUT, "graf_%s.png" % key)
        img.save(path, optimize=True)
        total += os.path.getsize(path)
        print("%-6s %4dx%-4d %s" % (key, img.width, img.height, text))
    path = os.path.join(OUT, "graf_eye.png")
    eye(7).save(path, optimize=True)
    total += os.path.getsize(path)
    print("%d pieces, %.1f MB" % (len(PIECES) + 1, total / 1048576.0))


if __name__ == "__main__":
    main()

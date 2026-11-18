"""Night Shift at Waygetter: decay decals (transparent PNGs the DLL embeds as assets/factory/dec_<key>.png):
water stains, mold, rust streaks, cracks, soot, cobwebs, dusty footprints, dead leaves, torn posters and
caution tape. All drawn here from noise and shapes (nothing downloaded).

  python tools/factory_decals.py
"""
import math
import os
import random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "assets", "factory")
F = r"C:\Windows\Fonts"


def noise(n, scale, seed):
    """Smooth value noise in 0..1: 'scale' cells across at the base octave, plus finer octaves."""
    rs = np.random.RandomState(seed)
    acc = np.zeros((n, n), np.float32)
    amp, tot = 1.0, 0.0
    for o in range(4):
        s = max(2, int(scale * 2 ** o))
        small = rs.rand(s, s).astype(np.float32)
        big = np.asarray(Image.fromarray((small * 255).astype(np.uint8)).resize((n, n), Image.BICUBIC), np.float32) / 255
        acc += big * amp
        tot += amp
        amp *= 0.5
    return acc / tot


def blob_mask(n, seed, softness=0.08):
    """An irregular blob with a ragged outline (like a real water mark), fading out before the edges."""
    y, x = np.mgrid[0:n, 0:n].astype(np.float32) / n - 0.5
    r = np.sqrt(x * x + y * y) * 2
    shape = noise(n, 2, seed) * 0.9 + noise(n, 6, seed + 9) * 0.35
    return np.clip((shape - r * 0.8 + 0.05) / softness, 0, 1)


def save(key, rgb, alpha):
    a = (np.clip(alpha, 0, 1) * 255).astype(np.uint8)
    img = np.dstack([np.clip(rgb, 0, 255).astype(np.uint8), a])
    Image.fromarray(img, "RGBA").save(os.path.join(OUT, "dec_%s.png" % key), optimize=True)


def stain(key, seed, color, n=256):
    """Water damage: a brown blotch with darker rings where it dried."""
    m = blob_mask(n, seed)
    inner = blob_mask(n, seed + 100, 0.1) * m                      # a second, smaller dried patch inside
    a = m * 0.28 * (0.6 + 0.4 * noise(n, 12, seed + 2)) + inner * 0.18
    edge = np.clip(m * (1 - m) * 4, 0, 1) * 0.55                    # the dark tide line where it dried
    rgb = np.zeros((n, n, 3), np.float32) + np.array(color, np.float32)
    save(key, rgb, a + edge)


def mold(key, seed, n=256):
    m = blob_mask(n, seed, 0.3)
    speck = (noise(n, 40, seed + 5) > 0.55).astype(np.float32)
    a = m * (0.25 + 0.75 * speck) * noise(n, 8, seed + 6) * 1.3
    rgb = np.zeros((n, n, 3), np.float32) + np.array((28, 36, 22), np.float32)
    save(key, rgb, a)


def rust(key, seed, w=96, h=320):
    """A rust streak running down from a point (a leaky pipe, a bolt)."""
    rs = random.Random(seed)
    img = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(img)
    for i in range(14):
        x = w / 2 + rs.uniform(-w * 0.25, w * 0.25)
        length = rs.uniform(0.4, 1.0) * h
        for k in range(int(length)):
            a = int(200 * (1 - k / length) ** 1.5)
            d.ellipse([x - 3, k - 3, x + 3, k + 3], fill=a)
            x += rs.uniform(-0.6, 0.6)
    a = np.asarray(img.filter(ImageFilter.GaussianBlur(3)), np.float32) / 255
    rgb = np.zeros((h, w, 3), np.float32) + np.array((120, 58, 22), np.float32)
    save(key, rgb, a * 0.8)


def crack(key, seed, n=256):
    """Branching cracks in plaster / concrete."""
    rs = random.Random(seed)
    img = Image.new("L", (n, n), 0)
    d = ImageDraw.Draw(img)
    def branch(x, y, ang, length, width, depth):
        for _ in range(int(length / 6)):
            nx, ny = x + math.cos(ang) * 6, y + math.sin(ang) * 6
            d.line([x, y, nx, ny], fill=255, width=max(1, int(width)))
            x, y = nx, ny
            ang += rs.uniform(-0.45, 0.45)
            if depth < 3 and rs.random() < 0.08:
                branch(x, y, ang + rs.choice([-1, 1]) * rs.uniform(0.5, 1.1), length * 0.5, width * 0.7, depth + 1)
    for _ in range(3):
        branch(n / 2, n / 2, rs.uniform(0, 6.28), n * 0.5, 3, 0)
    a = np.asarray(img.filter(ImageFilter.GaussianBlur(0.7)), np.float32) / 255
    rgb = np.zeros((n, n, 3), np.float32) + np.array((20, 18, 16), np.float32)
    save(key, rgb, a * 0.9)


def soot(key, seed, n=256):
    """Black soot / scorch, rising (darker at the bottom)."""
    m = blob_mask(n, seed, 0.5)
    y = np.linspace(1, 0.3, n)[:, None]
    a = m * noise(n, 6, seed + 3) * 1.4 * y
    rgb = np.zeros((n, n, 3), np.float32) + 10
    save(key, rgb, a)


def cobweb(key, seed, n=256):
    """A corner web: threads out from the corner (top left) and sagging rings between them."""
    rs = random.Random(seed)
    img = Image.new("L", (n, n), 0)
    d = ImageDraw.Draw(img)
    spokes = [rs.uniform(0.05, 1.5) for _ in range(9)] + [0.02, 1.55]
    spokes.sort()
    for a in spokes:
        d.line([0, 0, math.cos(a) * n * 1.2, math.sin(a) * n * 1.2], fill=170, width=1)
    for r in range(18, n, 16):
        pts = []
        for a in spokes:
            sag = r * rs.uniform(0.92, 1.0)
            pts.append((math.cos(a) * sag, math.sin(a) * sag))
        d.line(pts, fill=130, width=1)
    fade = blob_mask(n, seed, 0.6)
    y, x = np.mgrid[0:n, 0:n].astype(np.float32) / n
    corner = np.clip(1.3 - np.sqrt(x * x + y * y), 0, 1)
    a = np.asarray(img.filter(ImageFilter.GaussianBlur(0.5)), np.float32) / 255 * corner
    rgb = np.zeros((n, n, 3), np.float32) + 215
    save(key, rgb, a * 0.85)


def footprints(key, seed, w=128, h=512):
    """A trail of dusty shoe prints walking up the texture."""
    rs = random.Random(seed)
    img = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(img)
    for i in range(6):
        cx = w / 2 + (18 if i % 2 else -18) + rs.uniform(-3, 3)
        cy = h - 50 - i * 80
        d.ellipse([cx - 12, cy - 30, cx + 12, cy + 6], fill=150)     # sole
        d.ellipse([cx - 10, cy + 12, cx + 10, cy + 32], fill=150)    # heel
    a = np.asarray(img.filter(ImageFilter.GaussianBlur(2)), np.float32) / 255
    a *= 0.6 + 0.4 * noise(max(w, h), 20, seed)[:h, :w]
    rgb = np.zeros((h, w, 3), np.float32) + np.array((58, 52, 44), np.float32)
    save(key, rgb, a * 0.7)


def leaves(key, seed, n=256):
    """A drift of dead leaves."""
    rs = random.Random(seed)
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    for _ in range(60):
        L = Image.new("RGBA", (40, 24), (0, 0, 0, 0))
        c = rs.choice([(92, 58, 26), (120, 76, 30), (70, 50, 24), (130, 96, 40)])
        ImageDraw.Draw(L).ellipse([2, 4, 38, 20], fill=c + (230,))
        ImageDraw.Draw(L).line([4, 12, 38, 12], fill=(50, 34, 16, 230), width=1)
        L = L.rotate(rs.uniform(0, 360), expand=True)
        r = math.sqrt(rs.random()) * n * 0.42
        a = rs.uniform(0, 6.28)
        img.alpha_composite(L, (int(n / 2 + math.cos(a) * r - L.width / 2), int(n / 2 + math.sin(a) * r - L.height / 2)))
    img.save(os.path.join(OUT, "dec_%s.png" % key), optimize=True)


def tape(key):
    """CAUTION tape (repeats along its length)."""
    w, h = 512, 64
    img = Image.new("RGBA", (w, h), (235, 190, 20, 255))
    d = ImageDraw.Draw(img)
    font = ImageFont.truetype(os.path.join(F, "arialbd.ttf"), 38)
    for x in range(0, w, 256):
        d.text((x + 14, 10), "CAUTION", font=font, fill=(20, 18, 14, 255))
    d.rectangle([0, 0, w, 3], fill=(20, 18, 14, 255))
    d.rectangle([0, h - 4, w, h], fill=(20, 18, 14, 255))
    img.save(os.path.join(OUT, "dec_%s.png" % key), optimize=True)


def poster(key, seed, title, sub, bg, fg):
    """A torn, faded paper poster (safety / Tattletail ads)."""
    rs = random.Random(seed)
    w, h = 384, 512
    img = Image.new("RGBA", (w, h), bg + (255,))
    d = ImageDraw.Draw(img)
    big = ImageFont.truetype(os.path.join(F, "impact.ttf"), 64)
    small = ImageFont.truetype(os.path.join(F, "arialbd.ttf"), 26)
    d.rectangle([14, 14, w - 14, h - 14], outline=fg + (255,), width=6)
    d.multiline_text((w / 2, h * 0.3), title, font=big, fill=fg + (255,), anchor="mm", align="center")
    d.multiline_text((w / 2, h * 0.72), sub, font=small, fill=fg + (255,), anchor="mm", align="center")
    arr = np.asarray(img, np.float32)
    fadeamt = noise(w, 6, seed)[:h if h <= w else w, :w]
    fadeamt = np.asarray(Image.fromarray((noise(512, 6, seed) * 255).astype(np.uint8)).resize((w, h)), np.float32) / 255
    arr[..., :3] = arr[..., :3] * (0.65 + 0.35 * fadeamt[..., None]) + np.array((60, 50, 30)) * 0.25
    # Torn: bite irregular chunks out of two corners, and water-stain the bottom.
    alpha = np.ones((h, w), np.float32)
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    for cx, cy in ((w, 0), (0, h)):
        rr = np.sqrt((x - cx) ** 2 + (y - cy) ** 2) + np.asarray(Image.fromarray((noise(512, 10, seed + cx) * 255).astype(np.uint8)).resize((w, h)), np.float32) / 255 * 60
        alpha[rr < rs.uniform(90, 170)] = 0
    arr[..., 3] = alpha * 255
    arr[int(h * 0.8):, :, :3] *= 0.8
    Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA").save(os.path.join(OUT, "dec_%s.png" % key), optimize=True)


def main():
    os.makedirs(OUT, exist_ok=True)
    stain("stain1", 11, (70, 50, 26))
    stain("stain2", 12, (58, 44, 30))
    stain("stain3", 13, (40, 34, 26))
    mold("mold", 21)
    rust("rust", 31)
    crack("crack1", 41)
    crack("crack2", 42)
    soot("soot", 51)
    cobweb("web", 61)
    footprints("steps", 71)
    leaves("leaves", 81)
    tape("tape")
    poster("safety", 91, "SAFETY\nFIRST!", "Report every accident\nto your foreman.", (230, 214, 170), (160, 30, 24))
    poster("ad", 92, "HE REALLY\nEATS!", "Baby Talking Tattletail\nOnly at Waygetter", (120, 60, 150), (250, 220, 90))
    poster("care", 93, "HANDLE\nWITH CARE", "Every unit is\nsomeone's best friend.", (40, 70, 110), (235, 230, 210))
    poster("quiet", 94, "KEEP THEM\nQUIET", "A good Tattletail\nis a quiet Tattletail.", (215, 205, 180), (30, 30, 30))
    total = sum(os.path.getsize(os.path.join(OUT, f)) for f in os.listdir(OUT) if f.startswith("dec_"))
    print("decals: %.1f MB" % (total / 1048576.0))


if __name__ == "__main__":
    main()

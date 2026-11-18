"""
The Tattletail Pinball machine's art (src/Pinball*.cs):
  assets/pinball/backglass.jpg  - cropped from the user's concept art (tools/assets/pinball/concept.webp)
  assets/pinball/playfield.jpg  - the playfield, drawn here in table coordinates

Table coordinates (meters, same as src/PinballTable.cs): x across (-0.3..0.3, the player's right is +x),
y up the table (-0.02 at the front edge .. 1.18 at the back). The image covers exactly that, 2000 px per meter,
top of the image = the back of the table.

    python tools/pinball_art.py
"""
import math, os, random
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "assets", "pinball")
PX = 2000
X0, X1, Y0, Y1 = -0.3, 0.3, -0.02, 1.18
W, H = int((X1 - X0) * PX), int((Y1 - Y0) * PX)
XC = -0.0225  # the playfield's middle (the shooter lane takes the right edge)

def P(x, y):
    return ((x - X0) * PX, (Y1 - y) * PX)

def font(name, size):
    return ImageFont.truetype(os.path.join(os.environ.get("WINDIR", r"C:\Windows"), "Fonts", name), size)

def star(d, cx, cy, r, fill, inner=0.45, rot=-90):
    pts = []
    for k in range(10):
        rr = r if k % 2 == 0 else r * inner
        a = math.radians(rot + k * 36)
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
    d.polygon(pts, fill=fill)

def glow(img, x, y, r, color, alpha=150):
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ld = ImageDraw.Draw(layer)
    cx, cy = P(x, y)
    rp = r * PX
    ld.ellipse((cx - rp, cy - rp, cx + rp, cy + rp), fill=color + (alpha,))
    layer = layer.filter(ImageFilter.GaussianBlur(rp * 0.45))
    img.alpha_composite(layer)

def chevron(d, x, y, ang, size, fill):
    """An arrow insert pointing along ang (degrees, table space: 0 = +x, 90 = up the table)."""
    a = math.radians(ang)
    fx, fy = math.cos(a), math.sin(a)
    sx, sy = -fy, fx
    tip = (x + fx * size, y + fy * size)
    l = (x - fx * size * 0.2 + sx * size * 0.8, y - fy * size * 0.2 + sy * size * 0.8)
    r = (x - fx * size * 0.2 - sx * size * 0.8, y - fy * size * 0.2 - sy * size * 0.8)
    m = (x + fx * size * 0.25, y + fy * size * 0.25)
    d.polygon([P(*tip), P(*l), P(*m), P(*r)], fill=fill)

def playfield():
    img = Image.new("RGBA", (W, H))
    top, bot = (16, 14, 52), (48, 18, 78)
    d = ImageDraw.Draw(img)
    for row in range(H):
        t = row / (H - 1)
        d.line([(0, row), (W, row)], fill=tuple(int(top[i] + (bot[i] - top[i]) * t) for i in range(3)) + (255,))
    # A faint diamond pattern, like a quilt.
    for k in range(-40, 60):
        x = k * 0.05
        d.line([P(x, Y0), P(x + 1.2, Y1)], fill=(255, 255, 255, 5), width=2)
        d.line([P(x, Y0), P(x - 1.2, Y1)], fill=(255, 255, 255, 5), width=2)
    # Glows under the bumpers (purple, yellow, teal) and the House.
    glow(img, XC - 0.075, 0.76, 0.07, (200, 90, 255))
    glow(img, XC + 0.075, 0.76, 0.07, (255, 220, 60))
    glow(img, XC, 0.66, 0.07, (60, 230, 230))
    glow(img, XC, 0.98, 0.1, (255, 190, 90), 120)
    glow(img, -0.25, 0.68, 0.05, (255, 80, 200), 110)
    glow(img, 0.16, 0.66, 0.08, (80, 220, 255), 90)
    d = ImageDraw.Draw(img)
    # Stars everywhere.
    rnd = random.Random(1998)
    for _ in range(170):
        x, y = rnd.uniform(X0, X1), rnd.uniform(Y0, Y1)
        r = rnd.uniform(0.003, 0.009) * PX
        c = rnd.choice([(255, 214, 80), (255, 240, 200), (255, 150, 230), (160, 220, 255)])
        star(d, *P(x, y), r, c + (rnd.randint(110, 230),), rot=rnd.uniform(-110, -70))
    # Arrow inserts: to the House (yellow), up the ramp (pink), to the star targets (teal).
    for k in range(3):
        chevron(d, XC, 0.47 + k * 0.045, 90, 0.02, (255, 214, 60, 230 - k * 40))
    for k in range(3):
        chevron(d, -0.225 + k * -0.012, 0.45 + k * 0.045, 105, 0.018, (255, 90, 210, 230 - k * 40))
    for k in range(3):
        chevron(d, 0.07 + k * 0.022, 0.5 + k * 0.03, 50, 0.016, (70, 230, 255, 230 - k * 40))
    # Big stars in front of the targets.
    for k in range(4):
        t = (k + 0.5) / 4
        x, y = 0.11 + 0.09 * t - 0.03, 0.56 + 0.18 * t + 0.015
        star(d, *P(x, y), 0.013 * PX, (255, 214, 60, 200))
    # The shooter lane: darker, with arrows up it.
    d.rectangle([P(0.235, 0.8), P(0.28, Y0)], fill=(10, 8, 30, 255))
    for k in range(5):
        chevron(d, 0.2575, 0.12 + k * 0.12, 90, 0.012, (255, 255, 255, 90))
    # The logo, across the middle.
    f = font("comicbd.ttf", 118)
    text = "Tattletail"
    tw = d.textlength(text, font=f)
    cx, cy = P(XC, 0.4)
    for off, col in ((10, (120, 30, 110, 255)), (5, (255, 120, 200, 255)), (0, (255, 214, 80, 255))):
        d.text((cx - tw / 2 + off * 0.3, cy - 70 + off), text, font=f, fill=col)
    f2 = font("comicbd.ttf", 40)
    for line, y in (("Feed  \u2022  Play  \u2022  Be Good", 0.33),):
        tw = d.textlength(line, font=f2)
        cx, cy = P(XC, y)
        d.text((cx - tw / 2, cy - 20), line, font=f2, fill=(255, 190, 230, 230))
    # A little Tattletail face between the flippers (purple fur, ears, big blue eyes, a beak and a smile).
    fx, fy = P(XC, 0.08)
    s = 0.034 * PX
    for sx in (-1, 1):
        d.polygon([(fx + sx * s * 0.55, fy - s * 0.5), (fx + sx * s * 1.05, fy - s * 1.45), (fx + sx * s * 0.95, fy - s * 0.2)], fill=(120, 60, 170, 255))
        d.polygon([(fx + sx * s * 0.65, fy - s * 0.55), (fx + sx * s * 0.98, fy - s * 1.2), (fx + sx * s * 0.9, fy - s * 0.35)], fill=(240, 150, 190, 255))
    d.ellipse((fx - s, fy - s * 0.95, fx + s, fy + s * 0.95), fill=(130, 70, 185, 255))
    d.ellipse((fx - s * 0.7, fy - s * 0.35, fx + s * 0.7, fy + s * 0.8), fill=(250, 225, 190, 255))
    for sx in (-1, 1):
        ex = fx + sx * s * 0.38
        d.ellipse((ex - s * 0.26, fy - s * 0.42, ex + s * 0.26, fy + s * 0.1), fill=(255, 255, 255, 255))
        d.ellipse((ex - s * 0.17, fy - s * 0.33, ex + s * 0.17, fy + s * 0.02), fill=(50, 120, 220, 255))
        d.ellipse((ex - s * 0.08, fy - s * 0.24, ex + s * 0.08, fy - s * 0.07), fill=(10, 10, 20, 255))
        d.ellipse((ex - s * 0.12, fy - s * 0.3, ex - s * 0.02, fy - s * 0.2), fill=(255, 255, 255, 255))
    d.polygon([(fx - s * 0.12, fy + s * 0.14), (fx + s * 0.12, fy + s * 0.14), (fx, fy + s * 0.3)], fill=(240, 120, 150, 255))
    d.arc((fx - s * 0.3, fy + s * 0.15, fx + s * 0.3, fy + s * 0.6), 20, 160, fill=(120, 40, 60, 255), width=5)
    # The drain: dark.
    d.rectangle([P(-0.3, 0.02), P(0.235, Y0)], fill=(8, 6, 20, 255))
    img.convert("RGB").save(os.path.join(OUT, "playfield.jpg"), quality=90)

def backglass():
    im = Image.open(os.path.join(HERE, "assets", "pinball", "concept.webp")).convert("RGB")
    im.crop((158, 5, 598, 268)).resize((880, 526), Image.LANCZOS).save(os.path.join(OUT, "backglass.jpg"), quality=92)

if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    playfield()
    backglass()
    print("ok", W, H)

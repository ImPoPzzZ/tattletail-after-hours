"""
The Tattletail Air Hockey table's art (src/AirHockey.cs), in the style of the user's concept:
  assets/airhockey/surface.jpg - the playing surface (1000 px per meter: 1.9 m x 0.95 m). Right edge = the +x end
                                 (seat 0, red), top edge = +y (across). Logos read from each player's end.
  assets/airhockey/side.jpg    - the cabinet's long sides: purple, stars, the logo, "1v1 AIR HOCKEY", little houses.

    python tools/airhockey_art.py
"""
import math, os, random
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "assets", "airhockey")
FONTS = os.path.join(os.environ.get("WINDIR", r"C:\Windows"), "Fonts")
RED, BLUE = (255, 60, 70), (70, 140, 255)

def font(name, size):
    return ImageFont.truetype(os.path.join(FONTS, name), size)

def star(d, cx, cy, r, fill, rot=-90):
    pts = []
    for k in range(10):
        rr = r if k % 2 == 0 else r * 0.45
        a = math.radians(rot + k * 36)
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
    d.polygon(pts, fill=fill)

def tattletail_face(d, fx, fy, s):
    for sx in (-1, 1):
        d.polygon([(fx + sx * s * 0.55, fy - s * 0.5), (fx + sx * s * 1.05, fy - s * 1.45), (fx + sx * s * 0.95, fy - s * 0.2)], fill=(120, 60, 170))
        d.polygon([(fx + sx * s * 0.65, fy - s * 0.55), (fx + sx * s * 0.98, fy - s * 1.2), (fx + sx * s * 0.9, fy - s * 0.35)], fill=(240, 150, 190))
    d.ellipse((fx - s, fy - s * 0.95, fx + s, fy + s * 0.95), fill=(130, 70, 185))
    d.ellipse((fx - s * 0.7, fy - s * 0.35, fx + s * 0.7, fy + s * 0.8), fill=(250, 225, 190))
    for sx in (-1, 1):
        ex = fx + sx * s * 0.38
        d.ellipse((ex - s * 0.26, fy - s * 0.42, ex + s * 0.26, fy + s * 0.1), fill=(255, 255, 255))
        d.ellipse((ex - s * 0.17, fy - s * 0.33, ex + s * 0.17, fy + s * 0.02), fill=(50, 120, 220))
        d.ellipse((ex - s * 0.08, fy - s * 0.24, ex + s * 0.08, fy - s * 0.07), fill=(10, 10, 20))
        d.ellipse((ex - s * 0.12, fy - s * 0.3, ex - s * 0.02, fy - s * 0.2), fill=(255, 255, 255))
    d.polygon([(fx - s * 0.12, fy + s * 0.14), (fx + s * 0.12, fy + s * 0.14), (fx, fy + s * 0.3)], fill=(240, 120, 150))
    d.arc((fx - s * 0.3, fy + s * 0.15, fx + s * 0.3, fy + s * 0.6), 20, 160, fill=(120, 40, 60), width=max(2, int(s * 0.06)))

def logo(text, size, fill=(255, 214, 80)):
    f = font("comicbd.ttf", size)
    tmp = Image.new("RGBA", (int(size * len(text) * 0.7) + 40, int(size * 1.6)), (0, 0, 0, 0))
    d = ImageDraw.Draw(tmp)
    for off, col in ((6, (120, 30, 110, 255)), (3, (255, 120, 200, 255)), (0, fill + (255,))):
        d.text((20 + off * 0.3, 10 + off), text, font=f, fill=col)
    return tmp.crop(tmp.getbbox())

def surface():
    W, H = 1900, 950
    img = Image.new("RGBA", (W, H), (18, 16, 52, 255))
    d = ImageDraw.Draw(img)
    rnd = random.Random(7)
    for _ in range(140):
        star(d, rnd.uniform(0, W), rnd.uniform(0, H), rnd.uniform(4, 10), rnd.choice([(255, 214, 80), (255, 240, 200), (255, 150, 230)]) + (rnd.randint(90, 200),), rnd.uniform(-110, -70))
    # Air holes.
    for x in range(12, W, 25):
        for y in range(12, H, 25):
            d.ellipse((x - 2, y - 2, x + 2, y + 2), fill=(8, 8, 25, 255))
    # Each half tinted toward its player's color (right = seat 0 red, left = seat 1 blue).
    tint = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    td = ImageDraw.Draw(tint)
    td.rectangle((W // 2, 0, W, H), fill=RED + (22,))
    td.rectangle((0, 0, W // 2, H), fill=BLUE + (22,))
    img.alpha_composite(tint)
    d = ImageDraw.Draw(img)
    # Lines: the center line, the goal creases.
    d.line((W / 2, 0, W / 2, H), fill=(255, 90, 200, 255), width=10)
    for cx, col in ((W, RED), (0, BLUE)):
        d.ellipse((cx - 200, H / 2 - 200, cx + 200, H / 2 + 200), outline=col + (255,), width=8)
    # The middle: a Tattletail in a ring.
    d.ellipse((W / 2 - 190, H / 2 - 190, W / 2 + 190, H / 2 + 190), fill=(40, 22, 80, 255), outline=(255, 90, 200, 255), width=10)
    tattletail_face(d, W / 2, H / 2 + 20, 110)
    # The logo on each half, reading from that player's end.
    lg = logo("Tattletail", 110)
    img.alpha_composite(lg.rotate(90, expand=True), (int(W * 0.72 - lg.height / 2), int(H / 2 - lg.width / 2)))
    img.alpha_composite(lg.rotate(-90, expand=True), (int(W * 0.28 - lg.height / 2), int(H / 2 - lg.width / 2)))
    glow = img.filter(ImageFilter.GaussianBlur(1))
    glow.convert("RGB").save(os.path.join(OUT, "surface.jpg"), quality=90)

def side():
    W, H = 1400, 330
    img = Image.new("RGBA", (W, H), (70, 30, 110, 255))
    d = ImageDraw.Draw(img)
    for row in range(H):
        t = row / H
        d.line((0, row, W, row), fill=(int(70 - 30 * t), int(30 - 12 * t), int(110 - 40 * t), 255))
    rnd = random.Random(3)
    for _ in range(60):
        star(d, rnd.uniform(0, W), rnd.uniform(0, H), rnd.uniform(6, 16), (255, 214, 80, rnd.randint(150, 255)))
    # Little houses along the bottom, one window lit.
    for k in range(9):
        x = 40 + k * 160 + rnd.randint(-20, 20)
        w, h = rnd.randint(60, 90), rnd.randint(40, 70)
        d.rectangle((x, H - h, x + w, H), fill=(35, 15, 55, 255))
        d.polygon([(x - 8, H - h), (x + w / 2, H - h - 38), (x + w + 8, H - h)], fill=(35, 15, 55, 255))
        d.rectangle((x + w / 2 - 8, H - h + 14, x + w / 2 + 8, H - h + 30), fill=(255, 210, 90, 255))
    tattletail_face(d, 150, 150, 80)
    lg = logo("Tattletail", 120)
    img.alpha_composite(lg, (int(W / 2 - lg.width / 2), 40))
    f = font("comicbd.ttf", 54)
    t = "1v1 AIR HOCKEY"
    tw = d.textlength(t, font=f)
    d.text((W / 2 - tw / 2, 60 + lg.height), t, font=f, fill=(255, 190, 230, 255))
    f2 = font("comicbd.ttf", 40)
    for text, x in (("They're", W - 250), ("always", W - 230), ("watching...", W - 260)):
        pass
    d.text((W - 340, 90), "They're always\n  watching...", font=f2, fill=(255, 150, 230, 230))
    img.convert("RGB").save(os.path.join(OUT, "side.jpg"), quality=90)

if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    surface()
    side()
    print("ok")

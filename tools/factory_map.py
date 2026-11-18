"""Night Shift at Waygetter: the lobby's "YOU ARE HERE" facility map (assets/factory/map_c.jpg,
map_n.jpg). It hangs on the lobby's west wall, so it's drawn the way you see it standing there:
west is up, north is to the right. Room rectangles match FactoryLayout.cs (x 0..60 east, z 0..40 north).

  python tools/factory_map.py
"""
import os
import random
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "assets", "factory")
W, H = 1000, 600
PAD_T, PAD_B, PAD_L, PAD_R = 92, 40, 40, 40
F = r"C:\Windows\Fonts"

# name, x0, z0, x1, z1 (building meters), style
ROOMS = [
    ("LOBBY\n(SECURITY)", 0, 0, 14, 20, "room"),
    ("HALL", 6, 20, 14, 27, "room"),
    ("STAIRS", 0, 20, 6, 27, "room"),
    ("BREAK\nROOM", 0, 27, 14, 40, "room"),
    ("ASSEMBLY FLOOR", 14, 10, 42, 40, "big"),
    ("TESTING\nLAB", 14, 0, 30, 10, "room"),
    ("R&D", 30, 0, 42, 10, "rd"),
    ("WAREHOUSE\n(SHIPPING)", 42, 0, 60, 40, "big"),
]


def to_px(x, z):
    """Building (x, z) -> image (px, py): north (z) goes right, east (x) goes down (west is up)."""
    sx = (W - PAD_L - PAD_R) / 40.0
    sy = (H - PAD_T - PAD_B) / 60.0
    return PAD_L + z * sx, PAD_T + x * sy


def main():
    img = Image.new("RGB", (W, H), (22, 40, 70))
    d = ImageDraw.Draw(img)
    title = ImageFont.truetype(os.path.join(F, "arialbd.ttf"), 34)
    label = ImageFont.truetype(os.path.join(F, "arialbd.ttf"), 22)
    small = ImageFont.truetype(os.path.join(F, "arial.ttf"), 17)
    d.text((PAD_L, 18), "WAYGETTER ELECTRONICS", font=title, fill=(235, 225, 190))
    d.text((PAD_L, 58), "FACILITY MAP  -  GROUND FLOOR", font=small, fill=(200, 195, 170))
    d.text((W - 300, 58), "IN CASE OF FIRE, USE STAIRS", font=small, fill=(200, 195, 170))

    for name, x0, z0, x1, z1, style in ROOMS:
        ax, ay = to_px(x0, z0)
        bx, by = to_px(x1, z1)
        box = [min(ax, bx), min(ay, by), max(ax, bx), max(ay, by)]
        fill = (34, 62, 104) if style != "rd" else (90, 30, 34)
        d.rectangle(box, fill=fill, outline=(225, 220, 200), width=4)
        cx, cy = (box[0] + box[2]) / 2, (box[1] + box[3]) / 2
        if name.startswith("LOBBY"):
            cx, cy = to_px(10, 12)  # below the YOU ARE HERE marker
        d.multiline_text((cx, cy), name, font=label, fill=(240, 235, 215), anchor="mm", align="center")
        if style == "rd":
            d.text((cx, cy + 30), "AUTHORIZED STAFF ONLY", font=small, fill=(240, 150, 140), anchor="mm")

    # Doorways (gaps in the walls), the front door, and the docks.
    def gap_x(z, a, b):   # a doorway in a wall running along x (at z)
        p0 = to_px(a, z); p1 = to_px(b, z)
        d.line([p0, p1], fill=(22, 40, 70), width=8)
    def gap_z(x, a, b):   # in a wall running along z (at x)
        p0 = to_px(x, a); p1 = to_px(x, b)
        d.line([p0, p1], fill=(22, 40, 70), width=8)
    gap_x(20, 9.5, 11.5); gap_x(27, 9.5, 11.5); gap_z(14, 13, 15); gap_z(14, 33.5, 35.5)
    gap_x(10, 35, 37); gap_x(10, 19, 21); gap_z(42, 23.5, 27.5); gap_z(6, 22.6, 23.8)
    gap_x(0, 5.5, 7.5)
    for a, b in ((17, 20.5), (22, 25.5)):
        p = to_px(60, (a + b) / 2)
        d.text((p[0], p[1] + 16), "DOCK", font=small, fill=(230, 200, 90), anchor="mm")

    # YOU ARE HERE: just inside the front door.
    yx, yy = to_px(1.4, 7.2)
    d.ellipse([yx - 11, yy - 11, yx + 11, yy + 11], fill=(220, 30, 30), outline=(255, 255, 255), width=3)
    d.text((yx + 18, yy), "YOU ARE HERE", font=label, fill=(255, 210, 200), anchor="lm")
    ex, ey = to_px(6.5, 0)
    d.text((ex + 12, ey), "< ENTRANCE", font=small, fill=(230, 200, 90), anchor="lm")

    # Age it: yellowed, faded, scratched, a water stain.
    rnd = random.Random(1998)
    over = Image.new("RGB", (W, H), (120, 100, 60))
    img = Image.blend(img, over, 0.18)
    d = ImageDraw.Draw(img)
    for _ in range(90):
        x, y = rnd.randrange(W), rnd.randrange(H)
        d.line([x, y, x + rnd.randint(-60, 60), y + rnd.randint(-8, 8)], fill=(150, 145, 130), width=1)
    stain = Image.new("L", (W, H), 0)
    ImageDraw.Draw(stain).ellipse([640, 330, 900, 560], fill=90)
    stain = stain.filter(ImageFilter.GaussianBlur(40))
    img = Image.composite(Image.new("RGB", (W, H), (90, 75, 45)), img, stain)

    os.makedirs(OUT, exist_ok=True)
    img.save(os.path.join(OUT, "map_c.jpg"), quality=90)
    Image.new("RGB", (64, 64), (128, 128, 255)).save(os.path.join(OUT, "map_n.jpg"), quality=95)  # flat
    print("map ->", OUT)


if __name__ == "__main__":
    main()

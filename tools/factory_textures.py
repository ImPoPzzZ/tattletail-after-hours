"""Night Shift at Waygetter: turn the CC0 ambientCG packs in tools/assets/ambientcg into the
textures the DLL embeds (assets/factory/<key>_c.jpg color, <key>_n.jpg normal).

  python tools/factory_textures.py

Everything is 1024 px (512 for the small stuff) at JPEG quality 88 to keep the DLL small.
All ambientCG textures are CC0 (https://ambientcg.com), no credit needed (we credit anyway).
"""
import os
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "tools", "assets", "ambientcg")
OUT = os.path.join(ROOT, "assets", "factory")

# key used in code -> (ambientCG asset, size)
TEXTURES = {
    "concrete": ("Concrete042A", 1024),      # factory floor
    "block":    ("PaintedBricks004", 1024),  # painted block walls
    "lobby":    ("Tiles074", 1024),          # lobby / break room floor
    "ceiling":  ("OfficeCeiling001", 512),   # drop ceiling
    "steel":    ("CorrugatedSteel009", 1024),# warehouse walls, roof
    "plate":    ("DiamondPlate008A", 512),   # dock plates, steps
    "green":    ("PaintedMetal006", 1024),   # machines, doors, lockers
    "hazard":   ("PaintedMetal016", 512),    # yellow/black stripes
    "danger":   ("PaintedMetal017", 512),    # DANGER DO NOT ENTER (R&D)
    "darkwood": ("Wood051", 512),           # the security desk, cabinets
    "wood":     ("Wood092", 512),           # tables, the display case
    "chip":     ("Chipboard004", 512),      # crates, pallets
    "cardboard":("Cardboard004", 512),      # boxes
    "cork":     ("Cork002", 512),           # bulletin boards
    "fabric":   ("Fabric030", 512),         # chair seats
    "asphalt":  ("Asphalt033", 1024),       # the parking lot
    "dirt":     ("Ground037", 1024),        # weedy ground around the lot
}


def main():
    os.makedirs(OUT, exist_ok=True)
    total = 0
    for key, (asset, size) in TEXTURES.items():
        folder = os.path.join(SRC, asset)
        for suffix, tag in (("Color", "c"), ("NormalGL", "n")):
            src = os.path.join(folder, "%s_1K-JPG_%s.jpg" % (asset, suffix))
            dst = os.path.join(OUT, "%s_%s.jpg" % (key, tag))
            im = Image.open(src).convert("RGB")
            if im.size != (size, size):
                im = im.resize((size, size), Image.LANCZOS)
            im.save(dst, quality=88, optimize=True)
            total += os.path.getsize(dst)
    print("%d textures, %.1f MB -> %s" % (len(TEXTURES) * 2, total / 1048576.0, OUT))


if __name__ == "__main__":
    main()

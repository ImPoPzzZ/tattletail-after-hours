"""The 3.5 trailer thumbnail (1280x720): the new characters running up Main Street, Papa in a red medallion, the title and a RECALLED sticker in the trailer's 3D cartoon letters. Frames come out of the finished trailer
(ffmpeg), chosen where no caption or name card is on screen.

Usage: python make_thumbnail_35.py [trailer.mp4]
"""
import os, subprocess, sys, tempfile
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter
import imageio_ffmpeg

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_thumbnail import cartoon_text, cover   # the 3.3 script's letters

FF = imageio_ffmpeg.get_ffmpeg_exe()
TRAILER = sys.argv[1] if len(sys.argv) > 1 else r'D:\videos\TattletailAfterHours_3.5_Trailer.mp4'
OUT = r'D:\videos\TattletailAfterHours_3.5_Thumbnail'
W, H = 1280, 720


def frame(t):
    path = os.path.join(tempfile.gettempdir(), 'ke35_%s.png' % t)
    subprocess.run([FF, '-hide_banner', '-loglevel', 'error', '-y', '-ss', str(t), '-i', TRAILER, '-frames:v', '1', path], check=True)
    return Image.open(path).convert('RGB')


def main():
    friends = frame(33.3)     # all four running up Main Street (the caption bar is under the sticker)
    papa = frame(41.97)       # Papa before his name card

    # The whole street: the new characters in front.
    canvas = friends.resize((W, H), Image.LANCZOS)
    canvas = ImageEnhance.Brightness(canvas).enhance(1.05)
    canvas = ImageEnhance.Contrast(canvas).enhance(1.1)
    canvas = ImageEnhance.Color(canvas).enhance(1.15)
    # (the trailer's caption bar runs along the bottom: the road below it is painted over it, the sticker hides the rest)
    import numpy as np
    arr = np.asarray(canvas).astype(np.float32)
    top, bottom = arr[588:592].mean(axis=0), arr[658:662].mean(axis=0)   # (the road just above and just below it, blended down each column)
    for y in range(592, 658):
        k = (y - 592) / 66.0
        arr[y] = top * (1 - k) + bottom * k
    canvas = Image.fromarray(arr.astype(np.uint8))
    canvas = canvas.convert('RGBA')

    # Top right: Papa in a round medallion with a red glow.
    D = 300
    pap = cover(papa, (540, 0, 1380, 840), (D, D))
    pap = ImageEnhance.Contrast(pap).enhance(1.15)
    pap = ImageEnhance.Sharpness(pap).enhance(1.4)
    cx, cy = W - D // 2 - 18, D // 2 + 14
    glow = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    for i in range(30):
        r = D // 2 + 6 + i * 2
        gd.ellipse([cx - r, cy - r, cx + r, cy + r], outline=(255, 40, 30, max(0, 90 - i * 3)), width=3)
    canvas = Image.alpha_composite(canvas, glow.filter(ImageFilter.GaussianBlur(6)))
    disc = Image.new('L', (D, D), 0)
    ImageDraw.Draw(disc).ellipse([0, 0, D - 1, D - 1], fill=255)
    canvas.paste(pap, (cx - D // 2, cy - D // 2), disc)
    d = ImageDraw.Draw(canvas)
    d.ellipse([cx - D // 2 - 6, cy - D // 2 - 6, cx + D // 2 + 6, cy + D // 2 + 6], outline=(0, 0, 0, 255), width=14)
    d.ellipse([cx - D // 2 - 4, cy - D // 2 - 4, cx + D // 2 + 4, cy + D // 2 + 4], outline=(255, 255, 255, 255), width=8)

    # The title.
    t1 = cartoon_text('TATTLETAIL:', 104, (255, 240, 140), (255, 160, 20), side=(120, 55, 5))
    t2 = cartoon_text('AFTER HOURS', 104, (255, 240, 140), (255, 160, 20), side=(120, 55, 5))
    t1 = t1.rotate(3, resample=Image.BICUBIC, expand=True)
    t2 = t2.rotate(3, resample=Image.BICUBIC, expand=True)
    canvas.alpha_composite(t1, (6, -20))
    canvas.alpha_composite(t2, (6, 82))

    # The sticker: RECALLED, red, tilted, bottom left.
    s = cartoon_text('RECALLED', 112, (255, 255, 255), (255, 170, 160), outline=(200, 20, 30), side=(90, 5, 15))
    s = s.rotate(-6, resample=Image.BICUBIC, expand=True)
    canvas.alpha_composite(s, (-2, H - s.height + 22))

    # A small 3.5 badge on Papa's side.
    badge = cartoon_text('3.5', 84, (120, 255, 240), (20, 170, 200), outline=(255, 255, 255), side=(5, 60, 80))
    badge = badge.rotate(10, resample=Image.BICUBIC, expand=True)
    canvas.alpha_composite(badge, (W - badge.width + 10, H - badge.height + 18))

    out = canvas.convert('RGB')
    out.save(OUT + '.png')
    out.save(OUT + '.jpg', quality=92)
    print('saved', OUT + '.png')


if __name__ == '__main__':
    main()

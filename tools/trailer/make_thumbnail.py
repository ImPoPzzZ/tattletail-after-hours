"""The 3.3 trailer thumbnail (1280x720): sunny beach bar | Mama's jumpscare, split on a torn diagonal,
the title in the trailer's 3D cartoon letters, and a NOW HIRING! sticker."""
import os
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageEnhance

HERE = os.path.dirname(os.path.abspath(__file__))
W, H = 1280, 720
FONT_HAPPY = r'C:\Windows\Fonts\seguibl.ttf'   # Segoe UI Black, like the trailer's happy cards


def cover(img, box, size):
    return img.crop(box).resize(size, Image.LANCZOS)


def cartoon_text(text, size, top, bottom, outline=(255, 255, 255), side=(110, 60, 10), stroke=None, depth=None, font_path=FONT_HAPPY):
    """Chunky 3D letters: a top-to-bottom gradient fill, a thick outline, a stepped dark side, a soft shadow."""
    font = ImageFont.truetype(font_path, size)
    stroke = stroke or max(4, size // 11)
    depth = depth or max(5, size // 8)
    x0, y0, x1, y1 = font.getbbox(text, stroke_width=stroke)
    pad = stroke + depth + 20
    w, h = x1 - x0 + pad * 2, y1 - y0 + pad * 2
    org = (pad - x0, pad - y0)

    fill_mask = Image.new('L', (w, h), 0)
    ImageDraw.Draw(fill_mask).text(org, text, font=font, fill=255)
    out_mask = Image.new('L', (w, h), 0)
    ImageDraw.Draw(out_mask).text(org, text, font=font, fill=255, stroke_width=stroke, stroke_fill=255)

    layer = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    # A soft drop shadow under everything.
    shadow = Image.new('RGBA', (w, h), (0, 0, 0, 150))
    shadow_mask = out_mask.filter(ImageFilter.GaussianBlur(size // 14))
    layer.paste(shadow, (int(depth * 0.6), depth + 4), shadow_mask)
    # The 3D side: the outlined shape stepped down and to the right, darkest furthest back.
    for i in range(depth, 0, -1):
        k = 0.6 + 0.4 * (1 - i / depth)
        col = tuple(int(c * k) for c in side) + (255,)
        layer.paste(Image.new('RGBA', (w, h), col), (int(i * 0.45), i), out_mask)
    # The outline, then the gradient fill.
    layer.paste(Image.new('RGBA', (w, h), outline + (255,)), (0, 0), out_mask)
    grad = Image.new('RGBA', (w, h))
    gd = ImageDraw.Draw(grad)
    top_y, bot_y = org[1] + y0, org[1] + y1 - stroke
    for y in range(h):
        t = min(1, max(0, (y - top_y) / max(1, bot_y - top_y)))
        gd.line([(0, y), (w, y)], fill=tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)) + (255,))
    layer.paste(grad, (0, 0), fill_mask)
    # A little shine on the upper half of the letters.
    shine = Image.new('L', (w, h), 0)
    ImageDraw.Draw(shine).rectangle([0, 0, w, top_y + (bot_y - top_y) * 0.38], fill=70)
    shine = Image.composite(shine, Image.new('L', (w, h), 0), fill_mask)
    layer.paste(Image.new('RGBA', (w, h), (255, 255, 255, 255)), (0, 0), shine)
    return layer


def main():
    beach = Image.open(os.path.join(HERE, 'f_46.0.png')).convert('RGB')
    mama = Image.open(os.path.join(HERE, 'j_78.6.png')).convert('RGB')

    # Left: the beach, a touch sunnier. Right: Mama, pulled out of the dark.
    canvas = cover(beach, (140, 0, 1740, 900), (W, H))  # above the trailer's caption bar
    canvas = ImageEnhance.Color(canvas).enhance(1.3)
    canvas = ImageEnhance.Brightness(canvas).enhance(1.08)
    m = cover(mama, (330, 0, 1410, 1080), (760, 760))
    m = m.filter(ImageFilter.MedianFilter(5)).filter(ImageFilter.GaussianBlur(0.8))  # smooth the video's compression blocks first
    m = ImageEnhance.Brightness(m).enhance(2.1)
    m = ImageEnhance.Contrast(m).enhance(1.3)
    m = ImageEnhance.Sharpness(m).enhance(1.6)
    # A vignette: her edges fall into the dark.
    vig = Image.new('L', m.size, 0)
    ImageDraw.Draw(vig).ellipse([60, 40, m.width + 120, m.height + 60], fill=255)
    vig = vig.filter(ImageFilter.GaussianBlur(90))
    m = Image.composite(m, Image.new('RGB', m.size, (8, 0, 0)), vig)
    r, g, b = m.split()
    m = Image.merge('RGB', (r.point(lambda v: min(255, int(v * 1.12))), g, b))  # a little warmer, redder

    # The torn diagonal between them.
    split = [(760, 0), (742, 60), (770, 120), (735, 190), (760, 260), (720, 330), (748, 400), (705, 470), (730, 540), (690, 610), (712, 680), (680, 720)]
    mask = Image.new('L', (W, H), 0)
    ImageDraw.Draw(mask).polygon(split + [(W, H), (W, 0)], fill=255)
    canvas.paste(m, (W - 760, -20), mask.crop((W - 760, -20, W, 740)))
    # A red glow creeping out from Mama's side, and a dark edge on the far right.
    glow = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    for i in range(40):
        gd.line(split, fill=(255, 30, 30, max(0, 60 - i * 3)), width=10 + i * 3)
    canvas = Image.alpha_composite(canvas.convert('RGBA'), glow.filter(ImageFilter.GaussianBlur(8)))
    d = ImageDraw.Draw(canvas)
    d.line(split, fill=(0, 0, 0, 255), width=16)
    d.line(split, fill=(255, 255, 255, 255), width=8)

    # The title.
    t1 = cartoon_text('TATTLETAIL:', 112, (255, 240, 140), (255, 160, 20), side=(120, 55, 5))
    t2 = cartoon_text('AFTER HOURS', 112, (255, 240, 140), (255, 160, 20), side=(120, 55, 5))
    t1 = t1.rotate(3, resample=Image.BICUBIC, expand=True)
    t2 = t2.rotate(3, resample=Image.BICUBIC, expand=True)
    canvas.alpha_composite(t1, (6, -18))
    canvas.alpha_composite(t2, (6, 92))

    # The sticker: NOW HIRING!
    s = cartoon_text('NOW HIRING!', 96, (255, 255, 255), (255, 200, 225), outline=(230, 30, 110), side=(90, 10, 45))
    s = s.rotate(-7, resample=Image.BICUBIC, expand=True)
    canvas.alpha_composite(s, (-4, H - s.height + 26))

    # A small 3.3 badge on Mama's side.
    badge = cartoon_text('3.3', 78, (120, 255, 240), (20, 170, 200), outline=(255, 255, 255), side=(5, 60, 80))
    badge = badge.rotate(10, resample=Image.BICUBIC, expand=True)
    canvas.alpha_composite(badge, (W - badge.width + 10, H - badge.height + 18))

    out = canvas.convert('RGB')
    out.save(r'D:\videos\TattletailAfterHours_3.3_Thumbnail.png')
    out.save(r'D:\videos\TattletailAfterHours_3.3_Thumbnail.jpg', quality=92)
    print('saved')


if __name__ == '__main__':
    main()

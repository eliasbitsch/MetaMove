# App icon and Quest system splash for the MetaMove travel demo, drawn with Pillow.
#   python tools/branding/make_branding.py
# Writes unity-quest/Assets/MetaMove/Branding/{metamove_icon.png, metamove_splash.png}.
# Motif: the cyan speed ring of the in-app HUD (distance-based speed scaling) around an
# "M", on the dark glass of the UI kit.
import math
import os
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "unity-quest", "Assets", "MetaMove", "Branding")
FONT = os.path.join(ROOT, "unity-quest", "Assets", "MRTemplateAssets", "Fonts", "Inter", "Inter-Regular.ttf")

CYAN = (64, 209, 224)
AMBER = (255, 168, 41)
BG0, BG1 = (20, 28, 36), (6, 9, 12)
TEXT_HI = (245, 247, 255)
TEXT_LO = (158, 173, 191)


def radial_bg(size, inner, outer):
    w, h = size
    img = Image.new("RGB", size, outer)
    px = img.load()
    cx, cy, r = w / 2, h * 0.42, math.hypot(w, h) / 2
    for y in range(h):
        for x in range(w):
            t = min(1.0, math.hypot(x - cx, y - cy) / r)
            t = t * t
            px[x, y] = tuple(int(inner[i] * (1 - t) + outer[i] * t) for i in range(3))
    return img


def ring(draw, box, width, frac, color, track=(255, 255, 255, 26)):
    draw.arc(box, 0, 360, fill=track, width=width)
    start = -90
    end = start + 360 * frac
    draw.arc(box, start, end, fill=color + (255,), width=width)
    # round caps
    cx, cy = (box[0] + box[2]) / 2, (box[1] + box[3]) / 2
    rr = (box[2] - box[0]) / 2 - width / 2
    for a in (start, end):
        x, y = cx + rr * math.cos(math.radians(a)), cy + rr * math.sin(math.radians(a))
        draw.ellipse((x - width / 2, y - width / 2, x + width / 2, y + width / 2), fill=color + (255,))


def glow(layer, radius):
    g = layer.filter(ImageFilter.GaussianBlur(radius))
    return Image.alpha_composite(g, layer)


def bold_text(draw, xy, text, font, fill, anchor, stroke):
    draw.text(xy, text, font=font, fill=fill, anchor=anchor, stroke_width=stroke, stroke_fill=fill)


def icon(n=1024):
    img = radial_bg((n, n), BG0, BG1).convert("RGBA")
    fx = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(fx)
    m = int(n * 0.16)
    ring(d, (m, m, n - m, n - m), int(n * 0.075), 0.74, CYAN)
    fx = glow(fx, n * 0.02)
    img = Image.alpha_composite(img, fx)
    d = ImageDraw.Draw(img)
    f = ImageFont.truetype(FONT, int(n * 0.40))
    bold_text(d, (n / 2, n / 2 + n * 0.01), "M", f, TEXT_HI, "mm", int(n * 0.012))
    # the amber dot: the person the ring is measuring
    r = n * 0.035
    a = math.radians(-90 + 360 * 0.74 + 22)
    rr = (n - 2 * m) / 2 - n * 0.0375
    x, y = n / 2 + rr * math.cos(a), n / 2 + rr * math.sin(a)
    d.ellipse((x - r, y - r, x + r, y + r), fill=AMBER)
    return img.convert("RGB")


def splash(n=1024):
    img = Image.new("RGBA", (n, n), (0, 0, 0, 255))
    fx = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(fx)
    s = int(n * 0.30)
    cx, cy = n / 2, n * 0.40
    ring(d, (cx - s / 2, cy - s / 2, cx + s / 2, cy + s / 2), int(n * 0.026), 0.74, CYAN)
    fx = glow(fx, n * 0.012)
    img = Image.alpha_composite(img, fx)
    d = ImageDraw.Draw(img)
    bold_text(d, (cx, cy + n * 0.004), "M", ImageFont.truetype(FONT, int(n * 0.12)), TEXT_HI, "mm", 3)
    bold_text(d, (cx, n * 0.66), "MetaMove", ImageFont.truetype(FONT, int(n * 0.085)), TEXT_HI, "mm", 2)
    d.text((cx, n * 0.74), "careful when you are close, fast when you are not",
           font=ImageFont.truetype(FONT, int(n * 0.028)), fill=TEXT_LO, anchor="mm")
    return img.convert("RGB")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    icon().save(os.path.join(OUT, "metamove_icon.png"))
    splash().save(os.path.join(OUT, "metamove_splash.png"))
    print("branding written to", OUT)

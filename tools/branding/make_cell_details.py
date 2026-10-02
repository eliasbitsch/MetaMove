# Textures for the cell details, drawn with Pillow (+ qrcode):
#   qr_anchor.png     the anchor reference plate - the same payload the lab rig's
#                     QrAnchorCalibrator looks for, so the scenery shows the real marker
#   cell_monitor.png  the industrial PC's screen: the cell as the HUD sees it
#   python tools/branding/make_cell_details.py
import math
import os
import qrcode
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "unity-quest", "Assets", "MetaMove", "Textures", "Bari")
FONT = os.path.join(ROOT, "unity-quest", "Assets", "MRTemplateAssets", "Fonts", "Inter", "Inter-Regular.ttf")
PAYLOAD = "METAMOVE_ROBOT_BASE_01"   # QrAnchorCalibrator.expectedPayload

CYAN, OK, WARN = (64, 209, 224), (77, 230, 133), (255, 168, 41)
GLASS, TEXT_HI, TEXT_LO = (14, 18, 22), (245, 247, 255), (158, 173, 191)


def f(px):
    return ImageFont.truetype(FONT, px)


def qr_plate(n=1024):
    img = Image.new("RGB", (n, int(n * 1.22)), (236, 238, 240))
    d = ImageDraw.Draw(img)
    q = qrcode.QRCode(error_correction=qrcode.constants.ERROR_CORRECT_M, border=2, box_size=10)
    q.add_data(PAYLOAD)
    q.make(fit=True)
    code = q.make_image(fill_color="black", back_color="white").convert("RGB").resize((n - 160, n - 160), Image.NEAREST)
    img.paste(code, (80, 80))
    d.rectangle((0, 0, n - 1, img.height - 1), outline=(150, 154, 158), width=10)
    d.text((n / 2, n + 55), "SPATIAL ANCHOR", font=f(104), fill=(30, 36, 44), anchor="mm", stroke_width=2, stroke_fill=(30, 36, 44))
    d.text((n / 2, n + 160), "robot base reference", font=f(48), fill=(70, 78, 88), anchor="mm")
    return img


def ring(d, box, width, frac, color):
    d.arc(box, 0, 360, fill=(40, 48, 56), width=width)
    d.arc(box, -90, -90 + 360 * frac, fill=color, width=width)


def monitor(w=1280, h=800):
    img = Image.new("RGB", (w, h), GLASS)
    d = ImageDraw.Draw(img)
    d.rectangle((0, 0, w, 84), fill=(20, 26, 32))
    d.text((40, 42), "MetaMove", font=f(40), fill=TEXT_HI, anchor="lm", stroke_width=1, stroke_fill=TEXT_HI)
    d.text((250, 44), "Cell monitor  ·  Kitting cell 01", font=f(28), fill=TEXT_LO, anchor="lm")
    d.rounded_rectangle((w - 250, 22, w - 40, 62), radius=20, fill=(22, 60, 40))
    d.ellipse((w - 232, 34, w - 216, 50), fill=OK)
    d.text((w - 200, 42), "RUNNING", font=f(26), fill=OK, anchor="lm", stroke_width=1, stroke_fill=OK)

    # speed ring
    cx, cy, r = 250, 380, 150
    ring(d, (cx - r, cy - r, cx + r, cy + r), 26, 1.0, OK)
    d.text((cx, cy - 10), "100%", font=f(72), fill=TEXT_HI, anchor="mm", stroke_width=2, stroke_fill=TEXT_HI)
    d.text((cx, cy + 52), "SPEED", font=f(24), fill=TEXT_LO, anchor="mm")
    d.text((cx, cy + 205), "Speed and separation monitoring", font=f(22), fill=TEXT_LO, anchor="mm")

    # tiles
    tiles = [("Nearest person", "2.40 m", "CLEAR", OK), ("Slow zone", "< 2.0 m", "slowed down", WARN),
             ("Stop zone", "< 0.6 m", "standstill", (255, 82, 72)), ("Cycle time", "4.1 s", "per part", CYAN)]
    x0, y0 = 500, 130
    for i, (label, value, sub, col) in enumerate(tiles):
        x, y = x0 + (i % 2) * 380, y0 + (i // 2) * 200
        d.rounded_rectangle((x, y, x + 350, y + 170), radius=22, fill=(24, 31, 38))
        d.rectangle((x, y + 30, x + 6, y + 140), fill=col)
        d.text((x + 30, y + 40), label, font=f(24), fill=TEXT_LO, anchor="lm")
        d.text((x + 30, y + 95), value, font=f(52), fill=TEXT_HI, anchor="lm", stroke_width=1, stroke_fill=TEXT_HI)
        d.text((x + 30, y + 142), sub, font=f(22), fill=col, anchor="lm")

    # speed trace: full speed, a dip while someone walked up, recovery ramp
    gx, gy, gw, gh = 500, 560, 730, 170
    d.rounded_rectangle((gx - 20, gy - 40, gx + gw + 20, gy + gh + 30), radius=22, fill=(24, 31, 38))
    d.text((gx, gy - 14), "Speed factor, last 10 min", font=f(22), fill=TEXT_LO, anchor="lm")
    pts = []
    for i in range(121):
        t = i / 120
        v = 1.0
        if 0.45 < t < 0.62: v = 1.0 - 0.75 * math.sin((t - 0.45) / 0.17 * math.pi) ** 0.6
        if 0.53 < t < 0.56: v = 0.0
        pts.append((gx + t * gw, gy + gh - v * (gh - 20) - 10))
    d.line(pts, fill=CYAN, width=5, joint="curve")
    return img


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    qr_plate().save(os.path.join(OUT, "qr_anchor.png"))
    monitor().save(os.path.join(OUT, "cell_monitor.png"))
    print("written to", OUT)

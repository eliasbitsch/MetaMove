"""Generate a QR marker PNG + print-ready A4 PDF for MetaMove spatial-anchor tests.

Usage:
    python generate.py [payload] [size_mm]

Defaults:
    payload = METAMOVE_ROBOT_BASE_01
    size_mm = 100

Output:
    <payload>.png           — raw QR image (1480x1480 @ 300 DPI for 100mm, high ECC)
    <payload>_print.pdf     — A4 page, QR at exact physical size, 100mm scale bar below
    <payload>_print.png     — same layout as PDF, for preview

Print the PDF at 100% scale (disable "fit to page") — marker tracking accuracy depends on
the printed size matching `QrAnchorCalibrator.printedSizeMeters` in Unity.
"""
import sys
import qrcode
from qrcode.constants import ERROR_CORRECT_H
from PIL import Image, ImageDraw, ImageFont


def generate(payload: str, size_mm: int = 100) -> None:
    box = max(10, int(size_mm * 4))  # keeps output >=1000 px for crisp printing
    qr = qrcode.QRCode(
        version=None, error_correction=ERROR_CORRECT_H, box_size=box, border=4
    )
    qr.add_data(payload)
    qr.make(fit=True)
    img = qr.make_image(fill_color="black", back_color="white")
    img.save(f"{payload}.png")
    print(f"Saved {payload}.png  size={img.size}")

    # Print layout: A4, QR at exact physical size, with a 100mm calibration scale bar
    mm = 300 / 25.4
    a4 = (int(210 * mm), int(297 * mm))
    qr_px = int(size_mm * mm)
    page = Image.new("RGB", a4, "white")
    qr_scaled = img.convert("RGB").resize((qr_px, qr_px), Image.NEAREST)
    x = (a4[0] - qr_px) // 2
    y = int(50 * mm)
    page.paste(qr_scaled, (x, y))

    draw = ImageDraw.Draw(page)
    scale_y = y + qr_px + int(20 * mm)
    draw.line([(x, scale_y), (x + qr_px, scale_y)], fill="black", width=3)
    for i in range(0, (size_mm // 10) + 1):
        tick_x = x + int(i * 10 * mm)
        h = 20 if i % 5 == 0 else 10
        draw.line([(tick_x, scale_y - h), (tick_x, scale_y + h)], fill="black", width=3)
    try:
        font = ImageFont.truetype("arial.ttf", 40)
    except OSError:
        font = ImageFont.load_default()
    draw.text(
        (x, scale_y + int(10 * mm)),
        f"Scale: {size_mm} mm — measure after print to verify",
        fill="black",
        font=font,
    )
    draw.text(
        (x, y - int(15 * mm)),
        f"{payload}  —  print at 100% scale, no fit-to-page",
        fill="black",
        font=font,
    )

    page.save(f"{payload}_print.pdf", "PDF", resolution=300.0)
    page.save(f"{payload}_print.png")
    print(f"Saved {payload}_print.pdf and {payload}_print.png")


if __name__ == "__main__":
    payload = sys.argv[1] if len(sys.argv) > 1 else "METAMOVE_ROBOT_BASE_01"
    size_mm = int(sys.argv[2]) if len(sys.argv) > 2 else 100
    generate(payload, size_mm)

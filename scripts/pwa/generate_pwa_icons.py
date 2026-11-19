"""
Sinh bộ biểu tượng PWA cho Smart Study Workspace (mũ tốt nghiệp + tia sáng AI trên nền tím Indigo #4f46e5).

Một nguồn hình học duy nhất (hệ tọa độ 512x512) được xuất ra:
- icon.svg              : vector bo góc (favicon trình duyệt + manifest purpose "any")
- icon-512.png / 192.png: tràn viền (maskable) — nội dung nằm gọn trong vùng an toàn bán kính 40%
- apple-touch-icon.png  : 180x180 tràn viền cho iOS (iOS tự bo góc)
- favicon.png           : 64x64 bo góc nền trong suốt

Cách chạy (Pillow có sẵn trong venv của ai-service do pdfplumber phụ thuộc):
    ai-service\\.venv\\Scripts\\python.exe scripts\\pwa\\generate_pwa_icons.py
"""

from __future__ import annotations

import math
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter

PROJECT_ROOT = Path(__file__).resolve().parents[2]
ICONS_DIR = PROJECT_ROOT / "frontend" / "src" / "assets" / "icons"

BASE = 512
SUPERSAMPLE = 2048  # Vẽ ở độ phân giải cao rồi thu nhỏ LANCZOS để khử răng cưa sắc nét
ROUNDED_RADIUS = 112

COLOR_BG_START = "#4f46e5"
COLOR_BG_END = "#6d28d9"
COLOR_CAP_BASE = "#e0e7ff"
COLOR_BOARD_EDGE = "#a5b4fc"
COLOR_BOARD = "#ffffff"
COLOR_BUTTON = "#4338ca"
COLOR_CORD = "#fbbf24"
COLOR_TASSEL = "#f59e0b"
COLOR_SPARKLE_MAIN = "#67e8f9"
COLOR_SPARKLE_SECOND = "#fde68a"
COLOR_SPARKLE_DOT = "#c4b5fd"

Point = tuple[float, float]


# ---------------------------------------------------------------------------
# Hình học dùng chung (tọa độ trong khung 512x512)
# ---------------------------------------------------------------------------
def quadratic_curve(p0: Point, control: Point, p1: Point, steps: int = 24) -> list[Point]:
    points = []
    for i in range(steps + 1):
        t = i / steps
        x = (1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * control[0] + t ** 2 * p1[0]
        y = (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * control[1] + t ** 2 * p1[1]
        points.append((x, y))
    return points


def sparkle_star(center: Point, radius: float, sharpness: float = 2.4, steps: int = 96) -> list[Point]:
    """Ngôi sao 4 cánh cạnh lõm (dạng astroid) — biểu tượng tia sáng AI."""
    cx, cy = center
    points = []
    for i in range(steps):
        t = 2 * math.pi * i / steps
        cos_t, sin_t = math.cos(t), math.sin(t)
        x = cx + radius * math.copysign(abs(cos_t) ** sharpness, cos_t)
        y = cy + radius * math.copysign(abs(sin_t) ** sharpness, sin_t)
        points.append((x, y))
    return points


BOARD = [(96.0, 226.0), (256.0, 152.0), (416.0, 226.0), (256.0, 300.0)]
BOARD_EDGE = [(x, y + 13) for x, y in BOARD]
CAP_BASE = [(172.0, 246.0), (340.0, 246.0), (340.0, 330.0)] + quadratic_curve((340, 330), (256, 378), (172, 330))
CAP_BUTTON = ((256.0, 226.0), 11.0)
CORD = [(256.0, 226.0), (380.0, 250.0), (380.0, 318.0)]
CORD_WIDTH = 7.0
TASSEL_RECT = (369.0, 312.0, 391.0, 364.0, 8.0)
TASSEL_KNOT = ((380.0, 316.0), 9.5)
SPARKLE_MAIN = sparkle_star((360.0, 132.0), 42.0)
SPARKLE_SECOND = sparkle_star((166.0, 140.0), 20.0)
SPARKLE_DOT = ((410.0, 176.0), 7.0)
GLOW = ((256.0, 252.0), 178.0)


# ---------------------------------------------------------------------------
# Xuất SVG
# ---------------------------------------------------------------------------
def svg_points(points: list[Point]) -> str:
    return " ".join(f"{x:.1f},{y:.1f}" for x, y in points)


def build_svg(rounded: bool) -> str:
    radius = ROUNDED_RADIUS if rounded else 0
    (bx, by), button_r = CAP_BUTTON
    (kx, ky), knot_r = TASSEL_KNOT
    (dx, dy), dot_r = SPARKLE_DOT
    (gx, gy), glow_r = GLOW
    tx1, ty1, tx2, ty2, t_r = TASSEL_RECT
    return f"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {BASE} {BASE}" width="{BASE}" height="{BASE}">
  <title>Smart Study Workspace</title>
  <defs>
    <linearGradient id="ssBg" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="{COLOR_BG_START}"/>
      <stop offset="1" stop-color="{COLOR_BG_END}"/>
    </linearGradient>
    <radialGradient id="ssGlow" cx="0.5" cy="0.5" r="0.5">
      <stop offset="0" stop-color="#ffffff" stop-opacity="0.22"/>
      <stop offset="1" stop-color="#ffffff" stop-opacity="0"/>
    </radialGradient>
  </defs>
  <rect width="{BASE}" height="{BASE}" rx="{radius}" fill="url(#ssBg)"/>
  <circle cx="{gx}" cy="{gy}" r="{glow_r}" fill="url(#ssGlow)"/>
  <polygon points="{svg_points(CAP_BASE)}" fill="{COLOR_CAP_BASE}"/>
  <polygon points="{svg_points(BOARD_EDGE)}" fill="{COLOR_BOARD_EDGE}"/>
  <polygon points="{svg_points(BOARD)}" fill="{COLOR_BOARD}"/>
  <polyline points="{svg_points(CORD)}" fill="none" stroke="{COLOR_CORD}" stroke-width="{CORD_WIDTH}" stroke-linecap="round" stroke-linejoin="round"/>
  <circle cx="{bx}" cy="{by}" r="{button_r}" fill="{COLOR_BUTTON}"/>
  <rect x="{tx1}" y="{ty1}" width="{tx2 - tx1}" height="{ty2 - ty1}" rx="{t_r}" fill="{COLOR_TASSEL}"/>
  <circle cx="{kx}" cy="{ky}" r="{knot_r}" fill="{COLOR_CORD}"/>
  <polygon points="{svg_points(SPARKLE_MAIN)}" fill="{COLOR_SPARKLE_MAIN}"/>
  <polygon points="{svg_points(SPARKLE_SECOND)}" fill="{COLOR_SPARKLE_SECOND}"/>
  <circle cx="{dx}" cy="{dy}" r="{dot_r}" fill="{COLOR_SPARKLE_DOT}"/>
</svg>
"""


# ---------------------------------------------------------------------------
# Xuất PNG bằng Pillow (vẽ siêu mẫu 2048px rồi thu nhỏ)
# ---------------------------------------------------------------------------
def scale_points(points: list[Point], factor: float) -> list[Point]:
    return [(x * factor, y * factor) for x, y in points]


def circle_box(center: Point, radius: float, factor: float) -> list[float]:
    cx, cy = center
    return [(cx - radius) * factor, (cy - radius) * factor, (cx + radius) * factor, (cy + radius) * factor]


def diagonal_gradient(size: int, start_hex: str, end_hex: str) -> Image.Image:
    vertical = Image.linear_gradient("L").resize((size, size))
    horizontal = vertical.transpose(Image.Transpose.ROTATE_90).transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    diagonal_mask = ImageChops.add(vertical, horizontal, scale=2.0)
    start = Image.new("RGBA", (size, size), start_hex)
    end = Image.new("RGBA", (size, size), end_hex)
    return Image.composite(end, start, diagonal_mask)


def render_master(rounded: bool) -> Image.Image:
    size = SUPERSAMPLE
    factor = size / BASE
    canvas = diagonal_gradient(size, COLOR_BG_START, COLOR_BG_END)

    # Quầng sáng mềm phía sau mũ tốt nghiệp
    glow_layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    (gx, gy), glow_r = GLOW
    ImageDraw.Draw(glow_layer).ellipse(circle_box((gx, gy), glow_r * 0.62, factor), fill=(255, 255, 255, 58))
    glow_layer = glow_layer.filter(ImageFilter.GaussianBlur(radius=glow_r * factor * 0.32))
    canvas = Image.alpha_composite(canvas, glow_layer)

    draw = ImageDraw.Draw(canvas)
    draw.polygon(scale_points(CAP_BASE, factor), fill=COLOR_CAP_BASE)
    draw.polygon(scale_points(BOARD_EDGE, factor), fill=COLOR_BOARD_EDGE)
    draw.polygon(scale_points(BOARD, factor), fill=COLOR_BOARD)
    draw.line(scale_points(CORD, factor), fill=COLOR_CORD, width=round(CORD_WIDTH * factor), joint="curve")
    for end_point in (CORD[0], CORD[-1]):
        draw.ellipse(circle_box(end_point, CORD_WIDTH / 2, factor), fill=COLOR_CORD)
    button_center, button_r = CAP_BUTTON
    draw.ellipse(circle_box(button_center, button_r, factor), fill=COLOR_BUTTON)
    tx1, ty1, tx2, ty2, t_r = TASSEL_RECT
    draw.rounded_rectangle([tx1 * factor, ty1 * factor, tx2 * factor, ty2 * factor], radius=t_r * factor, fill=COLOR_TASSEL)
    knot_center, knot_r = TASSEL_KNOT
    draw.ellipse(circle_box(knot_center, knot_r, factor), fill=COLOR_CORD)
    draw.polygon(scale_points(SPARKLE_MAIN, factor), fill=COLOR_SPARKLE_MAIN)
    draw.polygon(scale_points(SPARKLE_SECOND, factor), fill=COLOR_SPARKLE_SECOND)
    dot_center, dot_r = SPARKLE_DOT
    draw.ellipse(circle_box(dot_center, dot_r, factor), fill=COLOR_SPARKLE_DOT)

    if rounded:
        mask = Image.new("L", (size, size), 0)
        ImageDraw.Draw(mask).rounded_rectangle([0, 0, size - 1, size - 1], radius=ROUNDED_RADIUS * factor, fill=255)
        canvas.putalpha(mask)

    return canvas


def save_png(master: Image.Image, size: int, file_name: str, opaque: bool) -> Path:
    image = master.resize((size, size), Image.Resampling.LANCZOS)
    if opaque:
        image = image.convert("RGB")
    output_path = ICONS_DIR / file_name
    image.save(output_path, format="PNG", optimize=True)
    return output_path


def main() -> None:
    ICONS_DIR.mkdir(parents=True, exist_ok=True)

    svg_path = ICONS_DIR / "icon.svg"
    svg_path.write_text(build_svg(rounded=True), encoding="utf-8")
    print(f"[OK] {svg_path.relative_to(PROJECT_ROOT)}")

    full_bleed_master = render_master(rounded=False)
    rounded_master = render_master(rounded=True)

    outputs = [
        save_png(full_bleed_master, 512, "icon-512.png", opaque=True),
        save_png(full_bleed_master, 192, "icon-192.png", opaque=True),
        save_png(full_bleed_master, 180, "apple-touch-icon.png", opaque=True),
        save_png(rounded_master, 64, "favicon.png", opaque=False),
    ]
    for path in outputs:
        with Image.open(path) as img:
            print(f"[OK] {path.relative_to(PROJECT_ROOT)} ({img.width}x{img.height}, {img.mode})")


if __name__ == "__main__":
    main()

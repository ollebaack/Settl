"""Generate public/favicon.ico from the Settl brand mark.

Mirrors public/favicon.svg exactly: a green rounded tile (brand --primary,
gradient #3f9d72 -> #2e7d5b) with a white round-capped "S", matching the in-app
logo (AuthLayout / Sidebar). Run after changing the mark:

    python apps/web/scripts/generate-favicon.py

Requires Pillow. No app dependency — this is a one-off asset build tool.
"""

from pathlib import Path

from PIL import Image, ImageDraw

# --- Geometry (512x512 design box, same numbers as favicon.svg) --------------
BOX = 512
CORNER = 140
STROKE = 48
TOP = (0x3F, 0x9D, 0x72)      # #3f9d72
BOTTOM = (0x2E, 0x7D, 0x5B)   # #2e7d5b

# S as two-and-a-half cubic beziers (start + three C segments in the SVG path).
S_POINTS = [
    (348, 176), (348, 128), (164, 128), (164, 208),
    (164, 268), (348, 244), (348, 312),
    (348, 392), (164, 392), (164, 340),
]

SS = 4  # supersample factor for crisp antialiasing


def lerp(a, b, t):
    return a + (b - a) * t


def cubic(p0, p1, p2, p3, t):
    u = 1 - t
    x = u * u * u * p0[0] + 3 * u * u * t * p1[0] + 3 * u * t * t * p2[0] + t * t * t * p3[0]
    y = u * u * u * p0[1] + 3 * u * u * t * p1[1] + 3 * u * t * t * p2[1] + t * t * t * p3[1]
    return x, y


def render(size):
    s = size * SS
    scale = s / BOX

    # Vertical brand gradient tile.
    grad = Image.new("RGB", (1, s))
    for y in range(s):
        t = y / (s - 1)
        grad.putpixel((0, y), tuple(round(lerp(TOP[i], BOTTOM[i], t)) for i in range(3)))
    grad = grad.resize((s, s))

    # Rounded-square alpha mask for the tile.
    mask = Image.new("L", (s, s), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, s - 1, s - 1], radius=CORNER * scale, fill=255)

    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    img.paste(grad, (0, 0), mask)

    # White "S": sample the beziers densely and stamp round dots -> round caps/joins.
    draw = ImageDraw.Draw(img)
    r = STROKE * scale / 2
    for seg in range(0, len(S_POINTS) - 1, 3):
        p0, p1, p2, p3 = S_POINTS[seg:seg + 4]
        for i in range(121):
            x, y = cubic(p0, p1, p2, p3, i / 120)
            cx, cy = x * scale, y * scale
            draw.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(255, 255, 255, 255))

    return img.resize((size, size), Image.LANCZOS)


def main():
    out = Path(__file__).resolve().parent.parent / "public" / "favicon.ico"
    sizes = [256, 128, 64, 48, 32, 16]  # largest first: Pillow uses imgs[0] as base
    imgs = [render(sz) for sz in sizes]
    imgs[0].save(out, format="ICO", sizes=[(sz, sz) for sz in sizes], append_images=imgs[1:])
    print(f"wrote {out} ({', '.join(f'{s}x{s}' for s in sizes)})")


if __name__ == "__main__":
    main()

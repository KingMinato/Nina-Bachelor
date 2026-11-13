"""Flat low-poly landscape paintings for the Holz room variant (framed on the walls by RoomVariantBuilder).

Writes Assets/Art/Environment/Paintings/<name>.png. Run: python Tools/paintings.py
No text, no people, no faces: decoration only, the same in every condition.
"""
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Art", "Environment", "Paintings")
W, H = 640, 480


def lerp(a, b, t):
    return tuple(int(round(x + (y - x) * t)) for x, y in zip(a, b))


def sky(img, top, bottom, horizon):
    d = ImageDraw.Draw(img)
    for y in range(H):
        d.line([(0, y), (W, y)], fill=lerp(top, bottom, min(1.0, y / horizon)))


def ridge(rng, base, height, peaks, jitter):
    """Polyline of a mountain ridge across the width: alternating peaks and saddles."""
    xs = np.linspace(-40, W + 40, peaks * 2 + 1)
    pts = []
    for i, x in enumerate(xs):
        up = height * (rng.uniform(0.7, 1.0) if i % 2 else rng.uniform(0.15, 0.4))
        pts.append((x + rng.uniform(-jitter, jitter), base - up))
    return pts


def faceted_range(img, rng, base, height, peaks, light, shade):
    """Mountain range, each slope a flat facet: lit on the left, shaded on the right (low-poly look)."""
    d = ImageDraw.Draw(img)
    pts = ridge(rng, base, height, peaks, 25)
    d.polygon(pts + [(W + 40, H), (-40, H)], fill=shade)
    for i in range(1, len(pts) - 1, 2):
        peak, left = pts[i], pts[i - 1]
        foot = (peak[0] + rng.uniform(-20, 10), base + 10)
        d.polygon([left, peak, foot, (left[0], base + 10)], fill=light)


def hills(img, rng, base, amp, colour, waves=2.0):
    d = ImageDraw.Draw(img)
    phase = rng.uniform(0, 6.3)
    xs = np.linspace(0, W, 24)
    pts = [(x, base - amp * (0.5 + 0.5 * np.sin(phase + waves * 2 * np.pi * x / W))) for x in xs]
    d.polygon(pts + [(W, H), (0, H)], fill=colour)


def pine(d, x, y, h, dark, light):
    w = h * 0.42
    d.rectangle([x - h * 0.03, y - h * 0.12, x + h * 0.03, y], fill=(92, 66, 46))
    for k in range(3):
        top = y - h * (0.35 + 0.3 * k) - h * 0.12
        bot = y - h * (0.3 * k) - h * 0.1
        ww = w * (1 - 0.25 * k)
        d.polygon([(x, top), (x + ww / 2, bot), (x - ww / 2, bot)], fill=dark)
        d.polygon([(x, top), (x, bot), (x - ww / 2, bot)], fill=light)


def canvas(img, rng):
    """Slight paint grain and soft edges, so it reads as a painting, not a screenshot."""
    a = np.asarray(img).astype(np.float32)
    a += rng.normal(0, 3.0, a.shape[:2])[..., None]
    img = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))
    return img.filter(ImageFilter.SMOOTH)


def mountains(seed=3):
    rng = np.random.default_rng(seed)
    img = Image.new("RGB", (W, H))
    sky(img, (122, 168, 204), (226, 222, 204), 300)
    d = ImageDraw.Draw(img)
    d.ellipse([440, 70, 510, 140], fill=(250, 236, 196))
    faceted_range(img, rng, 300, 190, 3, (196, 204, 214), (150, 162, 180))
    faceted_range(img, rng, 330, 140, 4, (124, 146, 132), (96, 118, 108))
    # lake with the far range mirrored faintly
    d.rectangle([0, 330, W, 400], fill=(118, 160, 186))
    for y in range(338, 396, 12):
        d.line([(rng.uniform(40, 200), y), (rng.uniform(260, 600), y)], fill=(150, 188, 206), width=3)
    hills(img, rng, 420, 40, (110, 140, 84), 1.2)
    hills(img, rng, 470, 30, (92, 122, 70), 1.7)
    d = ImageDraw.Draw(img)
    for x in (70, 105, 560, 595):
        pine(d, x, 450 + rng.uniform(-8, 8), 95 + rng.uniform(-10, 10), (56, 88, 62), (72, 108, 76))
    return canvas(img, rng)


def sunset(seed=7):
    rng = np.random.default_rng(seed)
    img = Image.new("RGB", (W, H))
    sky(img, (236, 166, 120), (250, 224, 170), 290)
    d = ImageDraw.Draw(img)
    d.ellipse([260, 190, 380, 310], fill=(252, 238, 190))
    hills(img, rng, 300, 60, (196, 132, 112), 1.0)
    hills(img, rng, 350, 55, (164, 108, 98), 1.4)
    hills(img, rng, 400, 50, (128, 96, 84), 1.9)
    hills(img, rng, 450, 35, (98, 80, 70), 2.3)
    d = ImageDraw.Draw(img)
    for x, y, h in ((120, 392, 70), (150, 398, 55), (500, 380, 80), (530, 388, 60)):
        pine(d, x, y, h, (84, 70, 62), (104, 86, 74))
    return canvas(img, rng)


def forest(seed=11):
    rng = np.random.default_rng(seed)
    img = Image.new("RGB", (W, H))
    sky(img, (150, 190, 214), (232, 236, 224), 260)
    d = ImageDraw.Draw(img)
    for cx, cy, r in ((150, 90, 34), (190, 80, 42), (232, 92, 30), (470, 120, 26), (500, 112, 34)):
        d.ellipse([cx - r, cy - r * 0.6, cx + r, cy + r * 0.6], fill=(246, 246, 240))
    hills(img, rng, 270, 50, (150, 176, 150), 1.3)
    d = ImageDraw.Draw(img)
    for x in np.linspace(-10, W + 10, 16):
        pine(d, x + rng.uniform(-12, 12), 300 + rng.uniform(-6, 6), 90 + rng.uniform(-15, 15), (96, 132, 108), (116, 150, 124))
    hills(img, rng, 360, 30, (128, 160, 96), 1.6)
    d = ImageDraw.Draw(img)
    for x in np.linspace(20, W - 20, 9):
        pine(d, x + rng.uniform(-20, 20), 410 + rng.uniform(-10, 10), 150 + rng.uniform(-25, 25), (52, 90, 64), (70, 110, 80))
    hills(img, rng, 470, 18, (106, 138, 76), 2.5)
    return canvas(img, rng)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for name, make in (("mountains", mountains), ("sunset", sunset), ("forest", forest)):
        path = os.path.join(OUT, name + ".png")
        make().save(path)
        print(os.path.normpath(path))

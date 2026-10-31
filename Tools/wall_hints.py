"""Draws the lever hints for Room 3: number 1-3 in a loose circle + up/down arrow, scribbled with charcoal on the wall.
Drawn from code, so the textures can be committed (no third-party art). Each drawing has its own random seed.

    python Tools/wall_hints.py [out_folder]

Writes hint_<n>_<up|down>.png (512 x 256) for the solution below, then run Mirror Game/Build Rooms.
"""
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parent.parent / "Assets" / "Art" / "Environment" / "Hints"
SS = 2  # supersampling

# Digit strokes in a unit box (x right, y down).
DIGITS = {
    1: [[(0.3, 0.24), (0.56, 0.05), (0.55, 0.96)]],
    2: [[(0.14, 0.3), (0.32, 0.08), (0.62, 0.06), (0.84, 0.26), (0.7, 0.55), (0.16, 0.94), (0.9, 0.93)]],
    3: [[(0.14, 0.16), (0.42, 0.04), (0.78, 0.12), (0.76, 0.38), (0.46, 0.48), (0.84, 0.6), (0.82, 0.86), (0.48, 0.97), (0.12, 0.86)]],
}

# Lever positions that open the door (true = up), must match RoomBuilder.Solution.
SOLUTION = [False, True, True]

# Charcoal look: line width (of the box height), passes per stroke, sideways wobble, shape jitter, overshoot, smudge.
ST = dict(width=0.085, passes=2, wobble=0.03, jitter=0.06, overshoot=0.08, smudge=0.18)


def smooth(points, n=28):
    """Catmull-Rom through the points."""
    p = [points[0]] + list(points) + [points[-1]]
    out = []
    for i in range(1, len(p) - 2):
        p0, p1, p2, p3 = map(np.array, p[i - 1:i + 3])
        for t in np.linspace(0, 1, n, endpoint=False):
            out.append(0.5 * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    out.append(np.array(points[-1], dtype=float))
    return np.array(out)


def dense(points, step=0.01):
    out = []
    for a, b in zip(points, points[1:]):
        a, b = np.array(a, dtype=float), np.array(b, dtype=float)
        n = max(2, int(np.linalg.norm(b - a) / step))
        out += [a + (b - a) * t for t in np.linspace(0, 1, n, endpoint=False)]
    out.append(np.array(points[-1], dtype=float))
    return np.array(out)


def arrow(up, r):
    """Shaft and head in a unit box, a little crooked."""
    bend = r.uniform(-0.12, 0.12)
    tip, tail = (0.5 + r.uniform(-0.05, 0.05), 0.04), (0.5 + r.uniform(-0.08, 0.08), 0.96)
    if not up:
        tip, tail = (tail[0], 0.96), (tip[0], 0.04)
    s = 1 if up else -1
    hy = tip[1] + s * r.uniform(0.26, 0.38)
    left = (0.5 - r.uniform(0.26, 0.4), hy + r.uniform(-0.06, 0.06))
    right = (0.5 + r.uniform(0.26, 0.4), hy + r.uniform(-0.06, 0.06))
    mid = ((tip[0] + tail[0]) / 2 + bend, 0.5)
    return [[tail, mid, tip], [left, tip, right]]


def circle(r, cx, cy, rx, ry):
    """Loose loop around the number, more than one turn, not closed neatly."""
    start = r.uniform(0, 2 * math.pi)
    turns = r.uniform(1.05, 1.3)
    pts = []
    for t in np.linspace(0, turns * 2 * math.pi, 50):
        k = 1 + 0.08 * math.sin(t * 1.7 + start) + t * 0.015
        pts.append((cx + rx * k * math.cos(t + start), cy + ry * k * math.sin(t + start)))
    return [pts]


def place(strokes, x, y, w, h):
    return [[(x + px * w, y + py * h) for px, py in s] for s in strokes]


def value_noise(w, h, cell, r):
    g = r.random((max(2, h // cell + 2), max(2, w // cell + 2)))
    img = Image.fromarray((g * 255).astype(np.uint8)).resize((w + 2 * cell, h + 2 * cell), Image.BICUBIC)
    return np.asarray(img, dtype=float)[cell:cell + h, cell:cell + w] / 255.0


def scribble(w, h, strokes, box, st, r):
    """Coverage mask of the strokes drawn by hand: shaky, pressed unevenly, traced several times, overshooting."""
    img = Image.new("L", (w * SS, h * SS), 0)
    d = ImageDraw.Draw(img)
    width = st["width"] * box
    for stroke in strokes:
        base = np.array(stroke, dtype=float)
        if len(base) < 12:  # every drawing a bit different (not the loops, they would get jagged)
            base += r.normal(0, st["jitter"] * box * 0.5, base.shape)
        for p in range(st["passes"]):
            pts = smooth(base) if len(base) > 3 else dense(base, box * 0.01)  # short strokes stay straight
            # overshoot: the hand runs on past the end before it lifts off
            over = st["overshoot"] * box * r.uniform(0.3, 1.0)
            direction = pts[-1] - pts[-2]
            direction /= max(1e-6, np.linalg.norm(direction))
            pts = np.vstack([pts, [pts[-1] + direction * over * k for k in np.linspace(0.1, 1, 6)]])
            # slow sideways drift (the hand) + offset of this pass
            n = len(pts)
            drift = np.cumsum(r.normal(0, 1, (n, 2)), axis=0)
            drift -= np.linspace(drift[0], drift[-1], n)
            drift *= st["wobble"] * box / max(1.0, np.abs(drift).max())
            pts = pts + drift + r.normal(0, 0.012 * box, 2)
            pressure = 0.6 + 0.4 * np.interp(np.arange(n), np.linspace(0, n, 6), r.random(6))
            taper = np.minimum(1, np.minimum(np.arange(n), n - 1 - np.arange(n)) / 6 + 0.35)
            for (x, y), pr, tp in zip(pts, pressure, taper):
                rad = width / 2 * pr * tp * (1.0 if p == 0 else r.uniform(0.55, 0.9))
                d.ellipse([(x - rad) * SS, (y - rad) * SS, (x + rad) * SS, (y + rad) * SS], fill=255)
    return np.asarray(img.resize((w, h), Image.LANCZOS), dtype=float) / 255.0


def charcoal(mask, st, r):
    """Grain with gaps where the charcoal skipped over the wall, a soft smudge around it."""
    h, w = mask.shape
    grain = value_noise(w, h, 2, r) * 0.55 + value_noise(w, h, 7, r) * 0.45
    streak = np.asarray(Image.fromarray((value_noise(w, h, 3, r) * 255).astype(np.uint8)).resize((w // 6, h)).resize((w, h)), dtype=float) / 255
    tex = np.clip((grain * 0.7 + streak * 0.3 - 0.1) * 2.4, 0, 1)
    smudge = np.asarray(Image.fromarray((mask * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(max(2, w / 60))), dtype=float) / 255
    a = mask * tex * 0.93 + smudge * st["smudge"] * value_noise(w, h, 18, r)
    rgb = np.broadcast_to(np.array([0.09, 0.085, 0.08]), (h, w, 3))
    return Image.fromarray((np.dstack([rgb, np.clip(a, 0, 1)]) * 255).astype(np.uint8), "RGBA")


def hint(n, up, st, r, W=512, H=256):
    box = H * r.uniform(0.66, 0.8) * 0.8  # room for the loop
    top = (H - box) / 2 + r.uniform(-0.06, 0.06) * H
    nx = W * r.uniform(0.1, 0.18)
    ax = W * r.uniform(0.54, 0.64)
    strokes = place(DIGITS[n], nx, top, box * r.uniform(0.55, 0.68), box)
    arrow_h = box * r.uniform(0.8, 1.0)
    strokes += place(arrow(up, r), ax, top + (box - arrow_h) / 2, box * r.uniform(0.5, 0.65), arrow_h)
    strokes += circle(r, nx + box * 0.31, top + box * 0.5, box * 0.5, box * 0.56)
    return charcoal(scribble(W, H, strokes, box, st, r), st, r)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    for n, up in enumerate(SOLUTION, 1):
        # seed 300 + ... is the drawing the developer approved (mockup 2026-10-31)
        hint(n, up, ST, np.random.default_rng(300 + n * 10 + up)).save(OUT / f"hint_{n}_{'up' if up else 'down'}.png")
    print("hints written to", OUT)

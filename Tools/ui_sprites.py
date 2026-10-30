"""Generates the sprites of the three UI styles (Pixel, Skizze, Holz) into Assets/Art/UI/Styles.

Run with: python Tools/ui_sprites.py   (needs numpy and Pillow)
Then run "Mirror Game/Build UI Styles" in Unity, which reads Styles/sprites.json for 9-slice borders and filtering.
Everything is drawn from code, so the sprites can be committed (no third-party art).
"""
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image

OUT = Path(__file__).resolve().parent.parent / "Assets" / "Art" / "UI" / "Styles"
META = {}


def rgba(hex_color, a=1.0):
    h = hex_color.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), int(round(a * 255)))


def save(style, name, img, border=(0, 0, 0, 0), point=False):
    """border = (left, bottom, right, top) in texels, like Unity's sprite border."""
    folder = OUT / style
    folder.mkdir(parents=True, exist_ok=True)
    img.save(folder / f"{name}.png")
    META[f"{style}/{name}.png"] = {"border": list(border), "point": point}


# ---------------------------------------------------------------- smooth drawing (supersampled)

class Canvas:
    """Premultiplied RGBA float canvas, supersampled ss times. Coordinates are in final texels, y down."""

    def __init__(self, w, h, ss=4):
        self.w, self.h, self.ss = w, h, ss
        ys, xs = np.mgrid[0:h * ss, 0:w * ss]
        self.x = (xs + 0.5) / ss
        self.y = (ys + 0.5) / ss
        self.p = np.zeros((h * ss, w * ss, 3))
        self.a = np.zeros((h * ss, w * ss))

    def paint(self, mask, color, alpha=None):
        """mask: bool or float coverage array. color: rgba tuple or HxWx3 array (0..1) with alpha argument."""
        if isinstance(color, tuple):
            c = np.array(color[:3]) / 255.0
            a = mask * (color[3] / 255.0)
        else:
            c = color
            a = mask * (1.0 if alpha is None else alpha)
        if alpha is not None and isinstance(color, tuple):
            a = a * alpha
        a = np.asarray(a, dtype=float)
        self.p = c * a[..., None] + self.p * (1 - a[..., None])
        self.a = a + self.a * (1 - a)

    def image(self):
        s = self.ss
        h, w = self.h, self.w
        p = self.p.reshape(h, s, w, s, 3).mean(axis=(1, 3))
        a = self.a.reshape(h, s, w, s).mean(axis=(1, 3))
        rgb = np.where(a[..., None] > 1e-6, p / np.maximum(a[..., None], 1e-6), 0)
        out = np.dstack([np.clip(rgb, 0, 1), np.clip(a, 0, 1)])
        return Image.fromarray((out * 255 + 0.5).astype(np.uint8), "RGBA")


def sd_round_rect(x, y, x0, y0, x1, y1, r):
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    hw, hh = (x1 - x0) / 2 - r, (y1 - y0) / 2 - r
    qx, qy = np.abs(x - cx) - hw, np.abs(y - cy) - hh
    return np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0) - r


def sd_circle(x, y, cx, cy, r):
    return np.hypot(x - cx, y - cy) - r


def sd_polyline(x, y, pts):
    d = np.full(x.shape, 1e9)
    for (ax, ay), (bx, by) in zip(pts[:-1], pts[1:]):
        dx, dy = bx - ax, by - ay
        t = np.clip(((x - ax) * dx + (y - ay) * dy) / max(dx * dx + dy * dy, 1e-9), 0, 1)
        d = np.minimum(d, np.hypot(x - ax - t * dx, y - ay - t * dy))
    return d


def sd_polygon(x, y, pts):
    """Signed distance to a closed polygon (negative inside)."""
    d = sd_polyline(x, y, pts + [pts[0]])
    inside = np.zeros(x.shape, dtype=bool)
    n = len(pts)
    for i in range(n):
        (ax, ay), (bx, by) = pts[i], pts[(i + 1) % n]
        cond = ((ay > y) != (by > y)) & (x < (bx - ax) * (y - ay) / (by - ay + 1e-12) + ax)
        inside ^= cond
    return np.where(inside, -d, d)


def wobble(c, seed, amp, freq):
    """Displaced sample coordinates: wavy, hand-drawn edges. Different seeds give the boiling frames."""
    rng = np.random.default_rng(seed)
    dx = np.zeros(c.x.shape)
    dy = np.zeros(c.x.shape)
    for k in (1, 2, 3):
        f = freq * k
        dx += amp / k * np.sin(c.y * f + rng.uniform(0, 6.28)) * np.cos(c.x * f * 0.37 + rng.uniform(0, 6.28))
        dy += amp / k * np.sin(c.x * f + rng.uniform(0, 6.28)) * np.cos(c.y * f * 0.41 + rng.uniform(0, 6.28))
    return c.x + dx, c.y + dy


def fill(d):
    return d <= 0


def ring(d, w):
    return np.abs(d) <= w / 2


def soft(d, blur):
    """Soft falloff outside a shape, for shadows and glows."""
    return np.clip(1 - np.maximum(d, 0) / blur, 0, 1) ** 2


def arc_points(cx, cy, r, a0, a1, n=48):
    """Angles in degrees, math orientation (counter-clockwise, y up), converted to image y down."""
    return [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * i / n)), cy - r * math.sin(math.radians(a0 + (a1 - a0) * i / n)))
            for i in range(n + 1)]


def turn_arrow(c, size, width, xy=None):
    """Curved arrow turning left (counter-clockwise): arc over the top, head on the left pointing down."""
    x, y = xy if xy else (c.x, c.y)
    s = size
    cx, cy, r = 0.5 * s, 0.58 * s, 0.27 * s
    pts = arc_points(cx, cy, r, -40, 180)
    end = pts[-1]
    head = [(end[0] - 0.15 * s, end[1] - 0.06 * s), end, (end[0] + 0.12 * s, end[1] - 0.1 * s)]
    return min_d(sd_polyline(x, y, pts), sd_polyline(x, y, head)) - width / 2


def up_arrow(c, size, width, xy=None):
    x, y = xy if xy else (c.x, c.y)
    s = size
    shaft = [(0.5 * s, 0.85 * s), (0.5 * s, 0.18 * s)]
    head = [(0.22 * s, 0.45 * s), (0.5 * s, 0.16 * s), (0.78 * s, 0.45 * s)]
    return min_d(sd_polyline(x, y, shaft), sd_polyline(x, y, head)) - width / 2


def min_d(*ds):
    out = ds[0]
    for d in ds[1:]:
        out = np.minimum(out, d)
    return out


# ---------------------------------------------------------------- pixel drawing (no anti-aliasing)

def px_canvas(w, h):
    return np.zeros((h, w, 4), dtype=np.uint8)


def px_rect(img, x0, y0, x1, y1, color):
    img[max(y0, 0):y1, max(x0, 0):x1] = color


def px_img(img):
    return Image.fromarray(img, "RGBA")


def px_art(rows, palette):
    h, w = len(rows), max(len(r) for r in rows)
    img = px_canvas(w, h)
    for yy, row in enumerate(rows):
        for xx, ch in enumerate(row):
            if ch in palette:
                img[yy, xx] = palette[ch]
    return img


def px_outline(img, color, pad=1):
    """Adds a one-texel outline around all opaque texels (4-neighbourhood), growing the image by pad."""
    h, w = img.shape[:2]
    out = px_canvas(w + 2 * pad, h + 2 * pad)
    out[pad:pad + h, pad:pad + w] = img
    solid = out[..., 3] > 0
    grow = solid.copy()
    grow[1:] |= solid[:-1]
    grow[:-1] |= solid[1:]
    grow[:, 1:] |= solid[:, :-1]
    grow[:, :-1] |= solid[:, 1:]
    out[grow & ~solid] = color
    return out


def px_scale(img, k):
    return np.kron(img, np.ones((k, k, 1), dtype=np.uint8))


# ================================================================ Pixel

def pixel():
    S = "Pixel"
    box, line, ink, yel, dim, drop = rgba("#2b2860"), rgba("#f4f4f4"), rgba("#1a1833"), rgba("#ffd23f"), rgba("#8a88b8"), rgba("#000000", 0.35)

    def panel(fill_c, line_c, bw=8, bh=8, shadow=5, o=2):
        """Box with a notched outline (the CSS box-shadow trick) and a hard drop shadow."""
        w, h = bw + 2 * o, bh + 2 * o + shadow
        img = px_canvas(w, h)
        px_rect(img, o, o + shadow, o + bw, o + bh + shadow, drop)
        px_rect(img, o, 0, o + bw, o + bh + 2 * o, line_c)
        px_rect(img, 0, o, o + bw + 2 * o, o + bh, line_c)
        px_rect(img, o, o, o + bw, o + bh, fill_c)
        return img, (o + 2, o + 2 + shadow, o + 2, o + 2)

    for name, f, l in (("box", box, line), ("btn", line, ink), ("btn_yel", yel, ink), ("box_yel", box, yel)):
        img, border = panel(f, l)
        save(S, name, px_img(img), border, point=True)

    # Highlight ring (wanted button, door outline): outline plus a translucent glow, centre empty.
    # Frame 0 yellow with glow, frame 1 white without: blinking like the CSS steps() animation.
    for i, (col, glow) in enumerate(((yel, rgba("#ffd23f", 0.35)), (line, (0, 0, 0, 0)))):
        g, o, b = 4, 2, 8
        w = b + 2 * (o + g)
        img = px_canvas(w, w)
        px_rect(img, 0, 0, w, w, glow)
        px_rect(img, g, g, w - g, w - g, (0, 0, 0, 0))
        px_rect(img, g + o, g, w - g - o, w - g, col)
        px_rect(img, g, g + o, w - g, w - g - o, col)
        px_rect(img, g + o, g + o, w - g - o, w - g - o, (0, 0, 0, 0))
        save(S, f"ring_{i}", px_img(img), (g + o + 1,) * 4, point=True)

    # Slider blocks, 46 x 60 px each at canvas scale (20 blocks fill the 920 px track).
    for name, on in (("block_off", False), ("block_on", True)):
        img = px_canvas(46, 60)
        if on:
            px_rect(img, 0, 0, 40, 60, yel)
            px_rect(img, 0, 52, 40, 60, rgba("#d9a514"))
        else:
            px_rect(img, 0, 0, 40, 60, dim)
            px_rect(img, 4, 4, 36, 56, ink)
        save(S, name, px_img(img), point=True)

    W = (255, 255, 255, 255)
    G = (190, 190, 205, 255)
    icons = {
        "arrow": ["....##......", "...###......", "..##########", ".###########", ".###########", "..##########", "...###......", "....##......"],
        "up": ["...##...", "..####..", ".######.", "########", "..####..", "..####..", "..####.."],
        "talk": [".##########.", "############", "##.##.##.###", "############", ".##########.", "..##........", ".#.........."],
        "tri": ["#...", "##..", "###.", "####", "###.", "##..", "#..."],
    }
    for name, rows in icons.items():
        art = px_art(rows, {"#": W})
        if name in ("arrow", "up"):
            art = px_outline(art, rgba("#1a1833"))
        save(S, name, px_img(px_scale(art, 4)), point=True)
    down = np.flipud(px_outline(px_art(icons["up"], {"#": W}), rgba("#1a1833")))
    save(S, "down", px_img(px_scale(down, 4)), point=True)

    coin = px_art(["..####..", ".#oooo#.", "#oo##oo#", "#o#oo#o#", "#o#oo#o#", "#oo##oo#", ".#oooo#.", "..####.."],
                  {"#": rgba("#b07d10"), "o": yel})
    save(S, "coin", px_img(px_scale(px_outline(coin, ink), 4)), point=True)
    star = px_art(["....#....", "....#....", "...###...", "#########", ".#######.", "..#####..", "..##.##..", ".##...##.", ".#.....#."], {"#": yel})
    save(S, "star", px_img(px_scale(px_outline(star, ink), 6)), point=True)

    # Loader square: frame 0 lit (yellow), frame 1 dark.
    for i, f in enumerate((yel, box)):
        img = px_canvas(10, 10)
        px_rect(img, 0, 0, 10, 10, line)
        px_rect(img, 1, 1, 9, 9, f)
        save(S, f"dot_{i}", px_img(px_scale(img, 3)), point=True)


# ================================================================ Skizze

def skizze():
    S = "Skizze"
    ink, paper, note, blue, red = rgba("#26221e"), rgba("#fffdf6"), rgba("#fff27a"), rgba("#cfe6ff"), rgba("#e2463b")
    shadow = rgba("#000000", 0.16)

    def paper_sheet(seed, fill_c, stroke=4.0, w=128, h=128, r=16, outline=True, sh=(5, 7)):
        c = Canvas(w, h)
        m0, m1 = 4, 12
        x, y = wobble(c, seed, 1.6, 0.09)
        d = sd_round_rect(x, y, m0, m0, w - m1, h - m1, r)
        ds = sd_round_rect(x - sh[0], y - sh[1], m0, m0, w - m1, h - m1, r)
        c.paint(fill(ds), shadow)
        c.paint(fill(d), fill_c)
        if outline:
            xs, ys = wobble(c, seed + 7, 1.2, 0.13)
            dl = sd_round_rect(xs, ys, m0, m0, w - m1, h - m1, r)
            c.paint(ring(dl, stroke), ink)
        return c.image()

    for i in range(3):
        save(S, f"paper_{i}", paper_sheet(10 + i, paper), (36, 40, 40, 36))
    save(S, "note", paper_sheet(3, note, outline=False, r=3, sh=(6, 9)), (24, 28, 28, 24))
    save(S, "blue", paper_sheet(4, blue, outline=False, r=3, sh=(6, 9)), (24, 28, 28, 24))

    def circle(seed, w=128, stroke=4.0, fill_c=paper, red_ring=False):
        c = Canvas(w, w)
        x, y = wobble(c, seed, 1.8, 0.08)
        rr = w / 2 - 10
        if red_ring:
            c.paint(ring(sd_circle(x, y, w / 2, w / 2, rr), stroke), red)
            return c.image()
        c.paint(fill(sd_circle(x - 4, y - 6, w / 2 - 2, w / 2 - 2, rr)), shadow)
        c.paint(fill(sd_circle(x, y, w / 2 - 2, w / 2 - 2, rr)), fill_c)
        xs, ys = wobble(c, seed + 3, 1.4, 0.12)
        c.paint(ring(sd_circle(xs, ys, w / 2 - 2, w / 2 - 2, rr), stroke), ink)
        return c.image()

    for i in range(3):
        save(S, f"circle_{i}", circle(20 + i))
        save(S, f"ring_{i}", circle(30 + i, w=160, stroke=5.0, red_ring=True))
        c = Canvas(128, 128)
        x, y = wobble(c, 40 + i, 1.8, 0.09)
        c.paint(ring(sd_round_rect(x, y, 8, 8, 120, 120, 18), 6.0), red)
        save(S, f"ringrect_{i}", c.image(), (36, 36, 36, 36))
        # scribbled slider rail
        c = Canvas(400, 24)
        rng = np.random.default_rng(50 + i)
        pts = [(4 + k * 392 / 24, 12 + rng.uniform(-2.2, 2.2)) for k in range(25)]
        c.paint(fill(sd_polyline(c.x, c.y, pts) - 2.2), ink)
        save(S, f"rail_{i}", c.image(), (10, 0, 10, 0))
        # loader circle
        c = Canvas(40, 40)
        x, y = wobble(c, 60 + i, 0.8, 0.12)
        c.paint(ring(sd_circle(x, y, 20, 20, 14), 4.0), ink)
        save(S, f"odot_{i}", c.image())

    # tape strip, beige label, highlighter
    c = Canvas(180, 42)
    rng = np.random.default_rng(5)
    jag = lambda side: [(side + rng.uniform(-4, 4), yy) for yy in np.linspace(0, 42, 8)]
    poly = [(p[0] + 6, p[1]) for p in jag(0)] + [(p[0] - 6, p[1]) for p in reversed(jag(180))]
    c.paint(fill(sd_polygon(c.x, c.y, poly)), rgba("#ffffff", 0.62))
    save(S, "tape", c.image())

    c = Canvas(200, 64)
    c.paint(fill(sd_polygon(c.x, c.y, [(4, 5), (196, 0), (200, 58), (2, 64)])), rgba("#f1e3b3", 0.95))
    save(S, "tag", c.image(), (14, 10, 14, 10))

    c = Canvas(64, 36)
    x, y = wobble(c, 8, 1.2, 0.2)
    c.paint(fill(sd_round_rect(x, y, 3, 4, 61, 32, 10)), rgba("#ffe240", 0.85))
    save(S, "hl", c.image(), (18, 14, 18, 14))

    # ruled notebook line (tiled vertically) and red margin
    c = Canvas(16, 60)
    c.paint((c.y >= 57) & (c.y < 60), rgba("#a9c9ea"))
    save(S, "lines", c.image())

    # icons, drawn in ink
    def icon(name, fn, size=128, color=ink, width=10.0, seed=70):
        c = Canvas(size, size)
        x, y = wobble(c, seed, 0.9, 0.15)
        c.paint(fill(fn(c, size, width, (x, y))), color)
        save(S, name, c.image())

    icon("turn", turn_arrow)
    icon("up", up_arrow)
    icon("down", lambda c, s, w, xy: up_arrow(c, s, w, (xy[0], s - xy[1])), color=red)

    def check(c, s, w, xy):
        x, y = xy
        return sd_polyline(x, y, [(0.16 * s, 0.55 * s), (0.4 * s, 0.78 * s), (0.86 * s, 0.22 * s)]) - w / 2
    icon("check", check, color=rgba("#2f9e44"), width=16.0)

    def dots(c, s, w, xy):
        x, y = xy
        return min_d(*[sd_circle(x, y, s * (0.25 + 0.25 * k), s * 0.5, s * 0.07) for k in range(3)])
    icon("dots", dots)


# ================================================================ Holz

def wood_color(c, base_top="#94602f", base_bottom="#6b4220", seed=1, vertical=True):
    t = np.clip(c.y / c.h, 0, 1)[..., None]
    a = np.array(rgba(base_top)[:3]) / 255.0
    b = np.array(rgba(base_bottom)[:3]) / 255.0
    col = a * (1 - t) + b * t
    rng = np.random.default_rng(seed)
    coord = c.x + 0.06 * c.y if vertical else c.y + 0.04 * c.x
    other = c.y if vertical else c.x
    warp = 2.5 * np.sin(other * 0.05 + rng.uniform(0, 6)) + 1.5 * np.sin(other * 0.13 + rng.uniform(0, 6))
    stripe = (np.mod(coord + warp, 19) < 2.2).astype(float) * 0.11
    fine = 0.035 * (np.mod(other, 7) < 1)
    return col * (1 - stripe[..., None]) + fine[..., None]


def parchment(c, cx=None, cy=None):
    cx = c.w / 2 if cx is None else cx
    cy = c.h * 0.4 if cy is None else cy
    r = np.hypot((c.x - cx) / (c.w / 2), (c.y - cy) / (c.h / 2))
    stops = [(0.0, "#f8ecd2"), (0.55, "#f8ecd2"), (1.0, "#e6cc96"), (1.35, "#caa468")]
    col = np.zeros(c.x.shape + (3,))
    for (p0, c0), (p1, c1) in zip(stops[:-1], stops[1:]):
        t = np.clip((r - p0) / (p1 - p0), 0, 1)[..., None]
        seg = (r >= p0) & ((r < p1) | (p1 == stops[-1][0]))
        mix = (np.array(rgba(c0)[:3]) * (1 - t) + np.array(rgba(c1)[:3]) * t) / 255.0
        col = np.where(seg[..., None], mix, col)
    return col


def holz():
    S = "Holz"
    dark, cream, gold, red = rgba("#3b2611"), rgba("#f6e7c4"), rgba("#e0b45a"), rgba("#8e1f1f")
    sh = rgba("#000000", 0.45)

    def nail(c, x, y, r=4.5):
        c.paint(fill(sd_circle(c.x, c.y, x, y + 1, r + 1.5)), rgba("#2e3035"))
        c.paint(fill(sd_circle(c.x, c.y, x, y, r)), rgba("#9a9da3"))
        c.paint(fill(sd_circle(c.x, c.y, x - 1.2, y - 1.2, r * 0.45)), rgba("#e3e3e3"))

    # plank with four nails, 9-slice
    w, h, m = 480, 128, 8
    c = Canvas(w, h + 10)
    c.paint(soft(sd_round_rect(c.x, c.y - 6, m, m, w - m, h - m, 10), 8), sh, 0.8)
    d = sd_round_rect(c.x, c.y, m, m, w - m, h - m, 8)
    c.paint(fill(d), wood_color(c), 1.0)
    c.paint(ring(d + 1.5, 3), dark)
    for nx, ny in ((m + 16, m + 16), (w - m - 16, m + 16), (m + 16, h - m - 16), (w - m - 16, h - m - 16)):
        nail(c, nx, ny)
    save(S, "plank", c.image(), (36, 46, 36, 36))

    # parchment panel in a wooden frame with nails in the corners
    w = h = 256
    m, fr = 10, 22
    c = Canvas(w, h + 8)
    c.paint(soft(sd_round_rect(c.x, c.y - 8, m, m, w - m, h - m, 12), 12), sh, 0.9)
    outer = sd_round_rect(c.x, c.y, m, m, w - m, h - m, 12)
    c.paint(fill(outer), wood_color(c, seed=3), 1.0)
    c.paint(ring(outer + 1.5, 3), dark)
    inner = sd_round_rect(c.x, c.y, m + fr, m + fr, w - m - fr, h - m - fr, 4)
    c.paint(fill(inner), parchment(c), 1.0)
    c.paint(soft(-inner, 14) * (inner <= 0), rgba("#78501e", 0.35))
    c.paint(ring(inner, 2.5), dark, 0.7)
    for nx, ny in ((m + 11, m + 11), (w - m - 11, m + 11), (m + 11, h - m - 11), (w - m - 11, h - m - 11)):
        nail(c, nx, ny)
    save(S, "panel", c.image(), (56, 64, 56, 56))

    # instruction scroll: parchment strip plus wooden rollers
    c = Canvas(256, 128)
    c.paint(np.ones(c.x.shape, dtype=bool), parchment(c), 1.0)
    c.paint((c.y < 3) | (c.y >= 125), rgba("#b8945a"))
    save(S, "scroll", c.image(), (24, 24, 24, 24))
    c = Canvas(40, 128)
    d = sd_round_rect(c.x, c.y, 2, 2, 38, 126, 18)
    t = np.abs(c.x - 20) / 20
    roll = (np.array(rgba("#c99b5d")[:3]) * (1 - t[..., None]) + np.array(rgba("#6f4a22")[:3]) * t[..., None]) / 255.0
    c.paint(fill(d), roll, 1.0)
    c.paint(ring(d + 1, 2), dark, 0.6)
    save(S, "roller", c.image(), (0, 24, 0, 24))

    # round shield with iron rim and four rivets
    s = 176
    c = Canvas(s, s + 10)
    cx = cy = s / 2
    c.paint(soft(sd_circle(c.x, c.y - 8, cx, cy, s / 2 - 6), 10), sh, 0.8)
    r = np.hypot(c.x - cx, c.y - cy)
    rings = np.where(r < 0.22 * 75, 0, np.where(r < 0.38 * 75, 1, np.where(r < 0.56 * 75, 0, 2)))
    cols = np.array([rgba("#a06c38")[:3], rgba("#89572b")[:3], rgba("#7d4d25")[:3]]) / 255.0
    c.paint(fill(sd_circle(c.x, c.y, cx, cy, 75)), cols[rings], 1.0)
    c.paint(ring(sd_circle(c.x, c.y, cx, cy, 79), 10), rgba("#5d6168"))
    c.paint(ring(sd_circle(c.x, c.y, cx, cy, 79), 10) & (c.y < cy - 40), rgba("#7d828b"), 0.6)
    c.paint(ring(sd_circle(c.x, c.y, cx, cy, 84.5), 1.6), rgba("#2e3035"))
    c.paint(ring(sd_circle(c.x, c.y, cx, cy, 73.5), 1.6), rgba("#2e3035"))
    for ang in (0, 90, 180, 270):
        nail(c, cx + 79 * math.cos(math.radians(ang)), cy + 79 * math.sin(math.radians(ang)), 4)
    save(S, "shield", c.image())

    # soft golden glow: frame (door, buttons) and circle (shields)
    c = Canvas(128, 128)
    d = sd_round_rect(c.x, c.y, 34, 34, 94, 94, 6)
    c.paint(soft(np.abs(d), 30), rgba("#ffc864"), 0.85)
    c.paint(ring(d, 5), rgba("#e0b45a"), 0.95)
    save(S, "glow", c.image(), (60, 60, 60, 60))
    c = Canvas(220, 220)
    d = sd_circle(c.x, c.y, 110, 110, 80)
    c.paint(soft(np.abs(d), 28), rgba("#ffc864"), 0.85)
    c.paint(ring(d, 6), rgba("#e0b45a"), 0.95)
    save(S, "glow_circle", c.image())

    # red banner with a notch at the bottom
    c = Canvas(320, 120)
    t = np.abs(c.x - 160) / 160
    col = (np.array(rgba("#9b2626")[:3]) * (1 - t[..., None]) + np.array(rgba("#6f1616")[:3]) * t[..., None]) / 255.0
    poly = [(8, 0), (312, 0), (312, 112), (160, 86), (8, 112)]
    c.paint(soft(sd_polygon(c.x, c.y - 5, poly), 6), sh, 0.6)
    c.paint(fill(sd_polygon(c.x, c.y, poly)), col, 1.0)
    c.paint(ring(sd_polyline(c.x, c.y, [(14, 0), (14, 102)]), 2) | ring(sd_polyline(c.x, c.y, [(306, 0), (306, 102)]), 2), gold, 0.7)
    save(S, "banner", c.image())

    # slider: gold rail, red gem knob, iron-ringed step shield reuses "shield"
    c = Canvas(128, 32)
    d = sd_round_rect(c.x, c.y, 2, 6, 126, 26, 10)
    t = np.clip((c.y - 6) / 20, 0, 1)[..., None]
    col = (np.array(rgba("#f2d08a")[:3]) * (1 - t) + np.array(rgba("#9a6b1f")[:3]) * t) / 255.0
    c.paint(fill(d), col, 1.0)
    c.paint(ring(d, 2.5), dark)
    save(S, "rail", c.image(), (14, 0, 14, 0))
    c = Canvas(96, 96)
    c.paint(soft(sd_circle(c.x, c.y - 4, 48, 48, 36), 8), sh, 0.7)
    c.paint(fill(sd_circle(c.x, c.y, 48, 48, 38)), gold)
    c.paint(ring(sd_circle(c.x, c.y, 48, 48, 38), 3), dark)
    r = np.hypot(c.x - 42, c.y - 40) / 34
    gem = (np.array(rgba("#ff6a5a")[:3]) * np.clip(1 - r, 0, 1)[..., None] + np.array(rgba("#a3151a")[:3]) * np.clip(r, 0, 1)[..., None]) / 255.0
    c.paint(fill(sd_circle(c.x, c.y, 48, 48, 29)), gem, 1.0)
    c.paint(fill(sd_circle(c.x, c.y, 39, 38, 7)), rgba("#ffffff", 0.75))
    save(S, "gem", c.image())

    # icons in cream with a dark drop shadow
    def icon(name, fn, size=128, width=13.0):
        c = Canvas(size, size)
        c.paint(fill(fn(c, size, width, (c.x, c.y - 5))), dark)
        c.paint(fill(fn(c, size, width, (c.x, c.y))), cream)
        save(S, name, c.image())

    icon("turn", turn_arrow)
    icon("up", up_arrow)

    def coin(c, s, cx, cy, r):
        rr = np.hypot(c.x - cx + r * 0.2, c.y - cy + r * 0.3) / r
        col = (np.array(rgba("#fff1b8")[:3]) * np.clip(1 - rr, 0, 1)[..., None] + np.array(rgba("#e0b45a")[:3]) * np.clip(rr, 0, 1)[..., None]) / 255.0
        c.paint(fill(sd_circle(c.x, c.y, cx, cy, r)), col, 1.0)
        c.paint(ring(sd_circle(c.x, c.y, cx, cy, r - 1.5), 3), rgba("#6b4a12"))
        c.paint(ring(sd_circle(c.x, c.y, cx, cy, r * 0.62), 2), rgba("#9a6b1f"), 0.7)

    c = Canvas(64, 64)
    coin(c, 64, 32, 32, 28)
    save(S, "coin", c.image())

    # coin pouch for the balance plaque
    c = Canvas(128, 128)
    body = sd_circle(c.x, c.y, 64, 80, 40)
    neck = sd_round_rect(c.x, c.y, 46, 30, 82, 50, 8)
    t = np.clip((c.y - 30) / 90, 0, 1)[..., None]
    col = (np.array(rgba("#b07a40")[:3]) * (1 - t) + np.array(rgba("#6b4220")[:3]) * t) / 255.0
    c.paint(fill(np.minimum(body, neck)), col, 1.0)
    c.paint(ring(np.minimum(body, neck), 3), dark)
    c.paint(ring(sd_polyline(c.x, c.y, [(44, 50), (84, 50)]), 5), gold)
    coin(c, 128, 92, 96, 18)
    save(S, "pouch", c.image())

    # wax seal with a check mark
    s = 192
    c = Canvas(s, s)
    ang = np.arctan2(c.y - s / 2, c.x - s / 2)
    rad = 80 + 5 * np.sin(ang * 14)
    d = np.hypot(c.x - s / 2, c.y - s / 2) - rad
    c.paint(soft(d - 2, 10), sh, 0.5)
    rr = np.hypot(c.x - 80, c.y - 76) / 90
    col = (np.array(rgba("#c8352e")[:3]) * np.clip(1 - rr, 0, 1)[..., None] + np.array(rgba("#6e1414")[:3]) * np.clip(rr, 0, 1)[..., None]) / 255.0
    c.paint(fill(d), col, 1.0)
    c.paint(ring(sd_circle(c.x, c.y, s / 2, s / 2, 56), 5), rgba("#5a0f0f"), 0.7)
    chk = sd_polyline(c.x, c.y, [(0.33 * s, 0.52 * s), (0.45 * s, 0.64 * s), (0.68 * s, 0.38 * s)]) - 9
    c.paint(fill(chk), rgba("#3a0909"), 0.85)
    save(S, "seal", c.image())

    # rope arc for the hanging door sign
    c = Canvas(160, 70)
    c.paint(ring(sd_circle(c.x, c.y, 80, 78, 70), 6) & (c.y < 70), rgba("#b08a52"))
    save(S, "rope", c.image())


if __name__ == "__main__":
    pixel()
    skizze()
    holz()
    sprites = [dict(path=path, **meta) for path, meta in META.items()]
    (OUT / "sprites.json").write_text(json.dumps({"sprites": sprites}, indent=1), encoding="utf-8")
    print(f"{len(META)} sprites -> {OUT}")

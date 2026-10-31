"""Renders the world sounds (footsteps, landing, doors, key, levers) to WAV files, the same for every UI style.

Physical sounds are built from damped resonances (modal synthesis), filtered noise and a short synthetic room reverb,
so they sound like objects in a room rather than UI beeps. Writes Assets/Art/UI/Sounds/World/<name>.wav,
then run Mirror Game/Build UI Styles (adds them to every style).

    python Tools/world_sounds.py
"""
import math

import numpy as np

from ui_sounds import OUT, RATE, write

rng = np.random.default_rng(11)


def t_axis(dur):
    return np.arange(int(RATE * dur)) / RATE


SPAN = int(RATE * 1.5)


def pad(x):
    """Every part is 1.5 s long (silence after it), so parts can simply be added."""
    return np.pad(x, (0, SPAN - len(x)))


def modal(freqs, decays, amps, dur=None):
    """Struck object: damped sines with random phase."""
    dur = dur or max(decays) * 6
    t = t_axis(dur)
    out = np.zeros_like(t)
    for f, d, a in zip(freqs, decays, amps):
        out += a * np.sin(2 * np.pi * f * t + rng.uniform(0, 2 * np.pi)) * np.exp(-t / d)
    attack = np.minimum(1, t / 0.0008)  # no click from the start of the sine
    return pad(out * attack)


def thump(f0, f1, dur, decay):
    t = t_axis(dur)
    f = f1 + (f0 - f1) * np.exp(-t / (dur / 4))
    return pad(np.sin(2 * np.pi * np.cumsum(f) / RATE) * np.exp(-t / decay) * np.minimum(1, t / 0.002))


def biquad(x, kind, f, q):
    w = 2 * math.pi * f / RATE
    cw, alpha = math.cos(w), math.sin(w) / (2 * q)
    if kind == "low":
        b = [(1 - cw) / 2, 1 - cw, (1 - cw) / 2]
    elif kind == "high":
        b = [(1 + cw) / 2, -(1 + cw), (1 + cw) / 2]
    else:
        b = [alpha, 0.0, -alpha]
    a = [1 + alpha, -2 * cw, 1 - alpha]
    y = np.zeros_like(x)
    x1 = x2 = y1 = y2 = 0.0
    for i in range(len(x)):
        out = (b[0] * x[i] + b[1] * x1 + b[2] * x2 - a[1] * y1 - a[2] * y2) / a[0]
        x2, x1, y2, y1 = x1, x[i], y1, out
        y[i] = out
    return y


def noise(dur, kind, f, q=0.7, decay=None, swell=None):
    """Filtered noise; decay: exponential fade, swell: (rise, fall) seconds."""
    t = t_axis(dur)
    x = biquad(rng.uniform(-1, 1, len(t)), kind, f, q)
    if decay:
        x *= np.exp(-t / decay)
    if swell:
        rise, fall = swell
        x *= np.minimum(1, t / rise) * np.clip((dur - t) / fall, 0, 1)
    return pad(x)


def creak(dur, rate0, rate1, resonances):
    """Stick-slip friction of a hinge: an uneven pulse train through narrow resonances."""
    n = int(RATE * dur)
    pulses = np.zeros(n)
    pos = 0.0
    while pos < dur:
        frac = pos / dur
        rate = rate0 + (rate1 - rate0) * math.sin(frac * math.pi)
        i = int(pos * RATE)
        if i < n:
            pulses[i] = rng.uniform(0.5, 1.0)
        pos += (1 / rate) * rng.uniform(0.85, 1.15)
    out = sum(biquad(pulses, "band", f, q) * g for f, q, g in resonances)
    t = t_axis(dur)
    return pad(out * np.sin(np.pi * np.clip(t / dur, 0, 1)) ** 1.5)


def mix(length, *parts):
    """parts: (start seconds, samples, gain)."""
    out = np.zeros(int(RATE * length))
    for at, s, g in parts:
        i = int(RATE * at)
        s = s[:len(out) - i]
        out[i:i + len(s)] += s * g
    return out


def room(x, wet, size=0.35):
    """Short synthetic reverb: decaying noise as impulse response, darkened."""
    t = t_axis(size)
    ir = rng.normal(0, 1, len(t)) * np.exp(-t / (size / 5))
    ir = biquad(ir, "low", 2500, 0.7)
    ir[: int(RATE * 0.008)] = 0  # first reflections arrive a little later
    ir /= np.sqrt(np.sum(ir ** 2))
    n = len(x) + len(ir)
    tail = np.fft.irfft(np.fft.rfft(x, n) * np.fft.rfft(ir, n), n)
    out = np.zeros(n)
    out[:len(x)] += x
    return out + tail * wet


def level(x, peak):
    fade = np.minimum(1, np.arange(len(x))[::-1] / (RATE * 0.02))  # no click at the end
    return x / np.max(np.abs(x)) * peak * fade


# ---------------------------------------------------------------- sounds

def step(k):
    """Shoe on a wooden floor: heel, floorboard, then the sole rolling off."""
    p = rng.uniform(0.9, 1.12)
    heel = (thump(115 * p, 70 * p, 0.12, 0.03) * 0.9 + noise(0.06, "low", 650 * p, decay=0.012) * 0.8
            + modal([190 * p, 430 * p, 760 * p], [0.045, 0.03, 0.02], [0.25, 0.18, 0.1]))
    sole = noise(0.07, "band", rng.uniform(1300, 2200), 0.9, swell=(0.01, 0.05)) * 0.18
    x = mix(0.3, (0, heel, rng.uniform(0.85, 1.0)), (rng.uniform(0.06, 0.09), sole, 1))
    return level(room(x, 0.12), 0.26 * rng.uniform(0.85, 1.0))


def land():
    heel = thump(95, 50, 0.25, 0.06) + noise(0.1, "low", 420, decay=0.02) * 0.9 + modal([160, 380, 690], [0.08, 0.05, 0.03], [0.3, 0.2, 0.1])
    second = thump(110, 60, 0.15, 0.03) * 0.5 + noise(0.05, "low", 500, decay=0.01) * 0.4
    cloth = noise(0.09, "band", 3000, 0.8, swell=(0.01, 0.07)) * 0.06
    x = mix(0.45, (0, heel, 1), (0.025, second, 1), (0, cloth, 1))
    return level(room(x, 0.15), 0.42)


def latch(f, decay=0.02, gain=1.0):
    return (modal([f, f * 1.62, f * 2.43, f * 3.7], [decay, decay * 0.8, decay * 0.6, decay * 0.4], [0.5, 0.35, 0.25, 0.12])
            + noise(0.02, "high", 3000, decay=0.003) * 0.3) * gain


def door_open():
    """Handle down, latch springs back, the hinge creaks softly while the door swings."""
    handle = latch(2300, 0.015, 0.6) + thump(260, 180, 0.06, 0.012) * 0.3
    swing = creak(0.95, 38, 70, [(820, 9, 1.0), (1650, 12, 0.6), (2700, 14, 0.3)]) * 0.5
    air = noise(1.0, "low", 260, swell=(0.3, 0.5)) * 0.25
    x = mix(1.4, (0, handle, 1), (0.09, latch(1500, 0.025), 0.9), (0.18, swing, 1), (0.15, air, 1))
    return level(room(x, 0.2), 0.36)


def door_close():
    """Door hits the frame: deep thud, the latch snaps in, a little rattle."""
    thud = (thump(78, 45, 0.4, 0.09) * 1.0 + modal([210, 340, 520], [0.07, 0.05, 0.035], [0.35, 0.22, 0.12])
            + noise(0.08, "low", 900, decay=0.015) * 0.7)
    x = mix(0.8, (0, thud, 1), (0.015, latch(1800, 0.02), 0.7), (0.07, latch(2600, 0.012), 0.25))
    return level(room(x, 0.28, 0.5), 0.6)


def unlock():
    """Key slides in, two pins click, the bolt turns back with a clunk."""
    scrape = noise(0.13, "band", 3600, 2.0, swell=(0.05, 0.06)) * 0.12
    pin = lambda f: modal([f, f * 1.55, f * 2.3], [0.008, 0.006, 0.004], [0.5, 0.3, 0.2])
    bolt = (modal([680, 1130, 1880, 2950], [0.06, 0.045, 0.03, 0.02], [0.45, 0.35, 0.22, 0.12])
            + thump(130, 85, 0.12, 0.03) * 0.45 + noise(0.04, "band", 1200, 1.0, decay=0.01) * 0.25)
    x = mix(0.8, (0, scrape, 1), (0.14, pin(3100), 0.7), (0.27, pin(2800), 0.7), (0.42, bolt, 1))
    return level(room(x, 0.12), 0.42)


def key_pick():
    """The key comes off its hook: a few small metal clinks."""
    parts = []
    for i in range(5):
        f = rng.uniform(2800, 5200)
        parts.append((i * 0.045 + rng.uniform(0, 0.02), modal([f, f * 2.76, f * 5.4], [0.12, 0.06, 0.03], [0.4, 0.2, 0.08]), rng.uniform(0.5, 1)))
    return level(room(mix(0.6, *parts), 0.1), 0.3)


def lever():
    """Iron lever: ratchet clicks while it turns, then it knocks into its stop."""
    click = lambda: modal([2500 * rng.uniform(0.95, 1.05), 4100], [0.006, 0.004], [0.5, 0.25])
    stop = (modal([430, 990, 1640, 2500], [0.08, 0.05, 0.035, 0.02], [0.45, 0.3, 0.2, 0.1])
            + thump(150, 95, 0.12, 0.03) * 0.5 + noise(0.03, "low", 1500, decay=0.008) * 0.3)
    x = mix(0.5, (0.0, click(), 0.6), (0.06, click(), 0.6), (0.12, click(), 0.6), (0.2, stop, 1))
    return level(room(x, 0.15), 0.42)


def mechanism():
    """Levers right: gears tick faster and faster inside the wall, then the bolt of the door slides back."""
    parts = []
    at = 0.0
    for i in range(9):
        f = 1900 * rng.uniform(0.92, 1.08)
        parts.append((at, modal([f, f * 1.7], [0.01, 0.006], [0.4, 0.2]) + thump(220, 160, 0.04, 0.008) * 0.2, 0.55))
        at += 0.075 - i * 0.005
    slide = noise(0.18, "band", 1100, 1.2, swell=(0.08, 0.06)) * 0.25
    bolt = (thump(100, 55, 0.35, 0.08) + modal([520, 870, 1430, 2300], [0.12, 0.08, 0.05, 0.03], [0.4, 0.3, 0.2, 0.1])
            + noise(0.06, "low", 800, decay=0.015) * 0.5)
    parts += [(at, slide, 1), (at + 0.17, bolt, 1)]
    return level(room(mix(at + 0.9, *parts), 0.25, 0.5), 0.5)


SOUNDS = {
    **{f"step_{k}": (lambda k=k: step(k)) for k in range(1, 5)},
    "land": land,
    "doorOpen": door_open,
    "doorClose": door_close,
    "unlock": unlock,
    "keyPick": key_pick,
    "lever": lever,
    "mechanism": mechanism,
}

if __name__ == "__main__":
    for name, make in SOUNDS.items():
        write(OUT / "World" / f"{name}.wav", make())
    print(len(SOUNDS), "world sounds")

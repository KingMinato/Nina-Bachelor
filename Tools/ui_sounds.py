"""Renders the UI sounds of the three UI styles (Pixel, Skizze, Holz) to WAV files.

The sounds are the WebAudio recipes of the HTML prototypes (Mockup Viewer, UI_Ausarbeitung): oscillator tones and
filtered noise bursts with exponential frequency and gain ramps. This script renders them offline the same way,
one clip per sound, into Assets/Art/UI/Sounds/<Style>/<name>.wav. Then run Mirror Game/Build UI Styles.

    python Tools/ui_sounds.py
"""
import math
import wave
from pathlib import Path

import numpy as np

RATE = 44100
OUT = Path(__file__).resolve().parent.parent / "Assets" / "Art" / "UI" / "Sounds"
rng = np.random.default_rng(7)


def tone(type="square", f=440, f2=None, dur=0.1, vol=0.15, at=0, attack=0.005):
    return dict(kind="tone", type=type, f=f, f2=f if f2 is None else f2, dur=dur, vol=vol, at=at, attack=attack)


def noise(dur=0.1, f=1000, f2=None, q=1, type="bandpass", vol=0.2, at=0):
    return dict(kind="noise", type=type, f=f, f2=f if f2 is None else f2, dur=dur, vol=vol, at=at, q=q)


def exp_ramp(a, b, n):
    """WebAudio exponentialRampToValueAtTime from a to b over n samples."""
    return a * (b / a) ** (np.arange(n) / max(1, n))


def render_tone(p):
    n = int(RATE * p["dur"])
    freq = exp_ramp(p["f"], max(1, p["f2"]), n)
    phase = np.cumsum(freq) / RATE
    wave_ = {
        "sine": lambda: np.sin(2 * np.pi * phase),
        "square": lambda: np.sign(np.sin(2 * np.pi * phase)),
        "triangle": lambda: 2 * np.abs(2 * (phase % 1) - 1) - 1,
        "sawtooth": lambda: 2 * (phase % 1) - 1,
    }[p["type"]]()
    na = max(1, int(RATE * p["attack"]))
    gain = np.concatenate([exp_ramp(0.0001, p["vol"], na), exp_ramp(p["vol"], 0.0001, n - na)])[:n]
    return wave_ * gain


def render_noise(p):
    n = int(RATE * p["dur"])
    x = rng.uniform(-1, 1, n)
    freq = exp_ramp(p["f"], max(1, p["f2"]), n)
    # Biquad as in WebAudio (Audio EQ Cookbook). Lowpass/highpass Q is in dB there, bandpass Q is linear.
    q = p["q"] if p["type"] == "bandpass" else 10 ** (p["q"] / 20)
    y = np.zeros(n)
    x1 = x2 = y1 = y2 = 0.0
    for i in range(n):
        w = 2 * math.pi * min(freq[i], RATE / 2 - 1) / RATE
        cw, alpha = math.cos(w), math.sin(w) / (2 * q)
        if p["type"] == "lowpass":
            b0, b1, b2 = (1 - cw) / 2, 1 - cw, (1 - cw) / 2
        elif p["type"] == "highpass":
            b0, b1, b2 = (1 + cw) / 2, -(1 + cw), (1 + cw) / 2
        else:
            b0, b1, b2 = alpha, 0.0, -alpha
        a0, a1, a2 = 1 + alpha, -2 * cw, 1 - alpha
        out = (b0 * x[i] + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2) / a0
        x2, x1, y2, y1 = x1, x[i], y1, out
        y[i] = out
    return y * exp_ramp(p["vol"], 0.0001, n)


def render(parts):
    length = int(RATE * (max(p["at"] + p["dur"] for p in parts) + 0.03))
    mix = np.zeros(length)
    for p in parts:
        s = render_tone(p) if p["kind"] == "tone" else render_noise(p)
        start = int(RATE * p["at"])
        mix[start:start + len(s)] += s
    return mix


def write(path, samples):
    path.parent.mkdir(parents=True, exist_ok=True)
    data = (np.clip(samples, -1, 1) * 32767).astype("<i2")
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(data.tobytes())


PIXEL = {
    "click": [tone(f=660, dur=.06, vol=.07)],
    "turn": [tone(f=220, f2=330, dur=.14, vol=.05)],
    "jump": [tone(f=260, f2=820, dur=.2, vol=.07)],
    "land": [tone(f=150, f2=80, dur=.08, vol=.06)],
    "count": [tone(f=880, dur=.06, vol=.07), tone(f=1320, dur=.09, vol=.07, at=.07)],
    "done": [tone(f=f, dur=.1, vol=.07, at=i * .08) for i, f in enumerate([523, 659, 784, 1046])],
    "instr": [tone(f=990, dur=.05, vol=.05), tone(f=1480, dur=.06, vol=.05, at=.05)],
    "panel": [tone(f=330, f2=660, dur=.1, vol=.05)],
    "type": [tone(f=1200, dur=.018, vol=.025)],
    "tick": [tone(f=1500, dur=.025, vol=.04)],
    "coin": [tone(f=988, dur=.07, vol=.07), tone(f=1319, dur=.3, vol=.07, at=.07)],
    "door": [noise(dur=.5, f=400, f2=150, type="lowpass", vol=.12)],
    "step": [tone(f=110, dur=.04, vol=.04)],
    "end": [tone(f=f, dur=.12, vol=.06, at=i * .1) for i, f in enumerate([1046, 784, 659, 523])],
}

SKIZZE = {
    "click": [tone(type="sine", f=540, f2=260, dur=.08, vol=.12)],
    "turn": [noise(dur=.28, f=2600, f2=1200, q=2, vol=.05)],
    "jump": [tone(type="triangle", f=280, f2=720, dur=.2, vol=.1)],
    "land": [noise(dur=.08, f=300, type="lowpass", vol=.12)],
    "count": [noise(dur=.05, f=3200, q=3, vol=.06), tone(type="sine", f=880, dur=.14, vol=.06, at=.03)],
    "done": [noise(dur=.06, f=3000, q=2, vol=.07, at=a) for a in (0, .07, .14)]
            + [tone(type="sine", f=660, dur=.3, vol=.07, at=.25), tone(type="sine", f=990, dur=.4, vol=.06, at=.37)],
    "instr": [noise(dur=.14, f=900, f2=200, type="lowpass", vol=.22)],
    "panel": [noise(dur=.32, f=1600, f2=500, q=1, vol=.09)],
    "type": [noise(dur=.016, f=4200, q=4, vol=.035)],
    "tick": [noise(dur=.02, f=3000, q=4, vol=.05)],
    "coin": [tone(type="sine", f=1480, dur=.18, vol=.06), tone(type="sine", f=1980, dur=.25, vol=.05, at=.06)],
    "door": [noise(dur=.5, f=500, f2=150, type="lowpass", vol=.12)],
    "step": [noise(dur=.05, f=220, type="lowpass", vol=.06)],
    "end": [tone(type="sine", f=f, dur=.3, vol=.05, at=i * .12) for i, f in enumerate([784, 659, 523])],
}

HOLZ = {
    "click": [tone(type="triangle", f=200, f2=120, dur=.1, vol=.22), noise(dur=.02, f=1800, q=2, vol=.06)],
    "turn": [noise(dur=.32, f=650, f2=250, type="lowpass", vol=.07)],
    "jump": [tone(type="triangle", f=200, f2=430, dur=.16, vol=.1)],
    "land": [tone(type="sine", f=95, f2=50, dur=.16, vol=.28)],
    "count": [tone(type="sine", f=2600, dur=.14, vol=.05), tone(type="sine", f=3400, dur=.1, vol=.04, at=.01)],
    "done": [tone(type="sine", f=130, f2=40, dur=.28, vol=.4), noise(dur=.12, f=450, type="lowpass", vol=.25),
             tone(type="sine", f=1568, dur=.4, vol=.05, at=.25), tone(type="sine", f=2093, dur=.5, vol=.045, at=.36)],
    "instr": [noise(dur=.55, f=1300, f2=450, q=.8, vol=.09)],
    "panel": [noise(dur=.38, f=800, f2=300, type="lowpass", vol=.11),
              tone(type="triangle", f=160, f2=100, dur=.12, vol=.15, at=.3)],
    "type": [noise(dur=.012, f=5200, type="highpass", vol=.02)],
    "tick": [tone(type="triangle", f=950, dur=.025, vol=.06)],
    "coin": [p for i, a in enumerate((0, .08, .17))
             for p in (tone(type="sine", f=2500 + i * 300, dur=.14, vol=.05, at=a),
                       tone(type="sine", f=3500 + i * 200, dur=.09, vol=.035, at=a + .005))],
    "door": [tone(type="sawtooth", f=110, f2=170, dur=.7, vol=.025),
             noise(dur=.5, f=400, f2=150, type="lowpass", vol=.1)],
    "step": [tone(type="sine", f=75, dur=.09, vol=.14)],
    "end": [tone(type="sine", f=f, dur=.5, vol=.045, at=i * .15) for i, f in enumerate([1568, 1319, 1047, 784])],
}

if __name__ == "__main__":
    for style, sounds in (("Pixel", PIXEL), ("Skizze", SKIZZE), ("Holz", HOLZ)):
        for name, parts in sounds.items():
            write(OUT / style / f"{name}.wav", render(parts))
        print(style, len(sounds), "sounds")

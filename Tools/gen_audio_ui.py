"""Extra UI sounds for the Venba-style popups (numpy + wave only, deterministic).
Outputs to Assets/Resources/Audio: paper, tape, stamp, page, pen, type, jingle_vn, jingle_kr, toggle, unlock."""
import os, wave
import numpy as np

SR = 44100
OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Assets", "Resources", "Audio")
rng = np.random.default_rng(2026)


def t(d): return np.arange(int(round(SR * d))) / SR


def env(n, a=0.005, r=0.08, shape=3.0):
    e = np.ones(n); na = max(1, int(a * SR)); nr = max(1, int(r * SR))
    e[:na] = np.linspace(0, 1, na)
    e[-nr:] *= np.linspace(1, 0, nr) ** shape
    return e


def lp(x, k):  # simple moving-average low-pass
    if k <= 1: return x
    return np.convolve(x, np.ones(k) / k, mode="same")


def hp(x, k): return x - lp(x, k)


def save(name, x, peak=0.85):
    x = np.asarray(x, dtype=np.float64)
    fade = int(0.004 * SR); x[:fade] *= np.linspace(0, 1, fade); x[-fade:] *= np.linspace(1, 0, fade)
    x = x / (np.max(np.abs(x)) + 1e-9) * peak
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((x * 32767).astype(np.int16).tobytes())
    print(name, round(len(x) / SR, 2), "s")


def noise(d): return rng.normal(0, 1, int(round(SR * d)))


def paper():  # crinkly sheet landing: bursts of band-passed crackle + soft thud
    d = 0.45; x = np.zeros(int(SR * d))
    for i in range(14):
        s = int(rng.uniform(0, 0.32) * SR); ln = int(rng.uniform(0.01, 0.05) * SR)
        b = hp(lp(rng.normal(0, 1, ln), 3), 25) * env(ln, 0.001, ln / SR * 0.8, 2) * rng.uniform(0.3, 1)
        x[s:s + ln] += b[: len(x[s:s + ln])]
    tt = t(d); x += 0.6 * np.sin(2 * np.pi * 90 * tt) * np.exp(-tt * 30) * (tt > 0.28) * np.exp(-(tt - 0.28).clip(0) * 25)
    return x


def tape():  # "xoẹt" – rising band-noise sweep
    d = 0.32; tt = t(d); n = noise(d); y = np.zeros_like(n)
    for i in range(len(n)):
        pass
    k = (8 + (1 - tt / d) * 30).astype(int)
    y = np.array([0.0] * len(n))
    # piecewise filter for speed
    for s in range(0, len(n), 512):
        seg = n[s:s + 512]; y[s:s + 512] = hp(seg, int(k[s])) if len(seg) > 2 else seg
    return y * env(len(y), 0.01, 0.12, 2) * (0.4 + tt / d)


def stamp():  # rubber stamp: low thump + wooden click
    d = 0.5; tt = t(d)
    thump = np.sin(2 * np.pi * (70 + 60 * np.exp(-tt * 40)) * tt) * np.exp(-tt * 14)
    click = hp(noise(d), 6) * np.exp(-tt * 120) * 0.6
    return thump + click


def page():  # page flip swoosh with a paper flick at the end
    d = 0.42; tt = t(d)
    sw = lp(noise(d), 6) * np.sin(np.pi * tt / d) ** 2 * 0.7
    flick = hp(noise(d), 12) * np.exp(-((tt - 0.33) * 60) ** 2) * 0.8
    return sw + flick


def pen():  # pencil scribble ~0.7s (looped by the game while writing)
    d = 0.7; tt = t(d); n = hp(noise(d), 10)
    am = 0.5 + 0.5 * np.sin(2 * np.pi * 9 * tt + 2 * np.sin(2 * np.pi * 2.3 * tt))
    return n * am * 0.6


def type_():  # soft typewriter tick
    d = 0.05; tt = t(d)
    return (hp(noise(d), 4) * np.exp(-tt * 260) + 0.4 * np.sin(2 * np.pi * 1800 * tt) * np.exp(-tt * 300))


def pluck(freq, d, bend=0.0, bright=0.5):  # Karplus-Strong with optional pitch bend (đàn bầu / gayageum)
    n = int(SR * d); out = np.zeros(n)
    period = SR / freq; buf = rng.uniform(-1, 1, int(period) + 2)
    pos = 0.0
    for i in range(n):
        p = period / (1 + bend * min(1, i / (SR * 0.25)))
        j = int(pos) % len(buf)
        out[i] = buf[j]
        nj = (j + 1) % len(buf)
        buf[j] = (bright * buf[j] + (1 - bright) * buf[nj]) * 0.996
        pos += len(buf) / p
    return out * env(n, 0.002, d * 0.6, 2)


def melody(notes, base, bend_style, bright):
    total = round(sum(dur for _, dur in notes) + 0.9, 3)
    x = np.zeros(int(round(SR * total))); cur = 0.0
    for semis, dur in notes:
        if semis is not None:
            f = base * 2 ** (semis / 12)
            p = pluck(f, dur + 0.6, bend=bend_style, bright=bright)
            s = int(cur * SR); x[s:s + len(p)] += p[: len(x) - s]
        cur += dur
    tt = t(total)
    pad = sum(np.sin(2 * np.pi * base * 2 ** (k / 12) * tt) for k in (0, 7, 12)) * 0.05 * np.sin(np.pi * tt / total)
    return x + pad


def jingle_vn():  # Vietnamese pentatonic, đàn bầu-like upward bends
    notes = [(0, .16), (2, .16), (4, .16), (7, .24), (9, .16), (7, .16), (12, .5)]
    return melody(notes, 392.0, 0.06, 0.45)


def jingle_kr():  # Korean pentatonic, gayageum-like plucks with downward vibrato feel
    notes = [(0, .14), (3, .14), (5, .14), (7, .22), (10, .14), (12, .14), (15, .5)]
    return melody(notes, 349.2, -0.03, 0.6)


def toggle():
    d = 0.09; tt = t(d)
    return np.sin(2 * np.pi * 1200 * tt) * np.exp(-tt * 70) + hp(noise(d), 5) * np.exp(-tt * 200) * 0.4


def unlock():  # tape peel + sparkle chime
    a = tape()[: int(SR * 0.22)]
    tt = t(0.5); c = sum(np.sin(2 * np.pi * f * tt) * np.exp(-tt * 7) for f in (1568, 2093, 2637)) * 0.35
    x = np.zeros(int(SR * 0.7)); x[: len(a)] += a; x[int(SR * 0.15): int(SR * 0.15) + len(c)] += c
    return x


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    save("paper", paper()); save("tape", tape()); save("stamp", stamp()); save("page", page())
    save("pen", pen(), 0.5); save("type", type_(), 0.5); save("jingle_vn", jingle_vn(), 0.8)
    save("jingle_kr", jingle_kr(), 0.8); save("toggle", toggle(), 0.6); save("unlock", unlock(), 0.8)

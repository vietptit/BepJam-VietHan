"""Deterministic cozy procedural audio. Run from any working directory."""
from pathlib import Path
import wave
import numpy as np

SR = 44100
RNG = np.random.default_rng(727)
OUT = Path(__file__).resolve().parents[1] / 'Assets/Resources/Audio'


def adsr(n, attack=.004, decay=.08, sustain=.25, release=.08):
    a, d, r = [min(int(x * SR), n // 3) for x in (attack, decay, release)]
    return np.concatenate((np.linspace(0, 1, a, endpoint=False),
                           np.linspace(1, sustain, d, endpoint=False),
                           np.full(n - a - d - r, sustain), np.linspace(sustain, 0, r)))


def bell(f, seconds=.6):
    t = np.arange(round(seconds * SR)) / SR
    return (np.sin(2*np.pi*f*t + .65*np.sin(2*np.pi*2*f*t)*np.exp(-18*t))
            * np.exp(-7*t/seconds) + .18*np.sin(2*np.pi*3*f*t)*np.exp(-15*t/seconds)) * np.minimum(t/.004, 1)


def mallet(f, seconds=.5):
    t = np.arange(round(seconds*SR))/SR
    return (np.sin(2*np.pi*f*t) + .25*np.sin(2*np.pi*4*f*t)*np.exp(-30*t))*np.exp(-9*t/seconds)


def pluck(f, seconds=.5):
    # Vectorized Karplus-Strong: progressively low-pass each traversal of a string.
    n = round(seconds*SR)
    period = max(2, round(SR/f))
    string = RNG.normal(0, .5, period)
    result = np.zeros(n)
    for start in range(0, n, period):
        size = min(period, n-start)
        result[start:start+size] = string[:size]
        string = .985*(string + np.roll(string, 1))*.5
    return result * np.exp(-3*np.arange(n)/n) * np.minimum(np.arange(n)/(.004*SR), 1)


def noise(seconds, low=300, high=6000):
    n = round(seconds*SR)
    x = RNG.normal(size=n)
    freq = np.fft.rfftfreq(n, 1/SR)
    filt = (1-np.exp(-(freq/low)**4))*np.exp(-(freq/high)**4)
    x = np.fft.irfft(np.fft.rfft(x)*filt, n)
    return x / max(np.max(np.abs(x)), 1e-9)


def air(seconds, rising=False):
    u = np.linspace(0, 1, round(seconds*SR))
    position = u if rising else 1-u
    return (noise(seconds, 200, 1500)*(1-position)
            + noise(seconds, 1800, 6500)*position)*np.sin(np.pi*u)**2


def sad_pluck(f, seconds):
    x = pluck(f, seconds)
    t = np.arange(len(x))/SR
    positions = np.clip(np.arange(len(x))+12*np.sin(2*np.pi*4*t), 0, len(x)-1)
    return np.interp(positions, np.arange(len(x)), x)


def sweep(f0, f1, seconds):
    t = np.arange(round(seconds*SR))/SR
    return np.sin(2*np.pi*(f0*t+(f1-f0)*t*t/(2*seconds))) * adsr(len(t), .004, .04, .15, .04)


def add(dst, sound, at=0, gain=1):
    start = round(at*SR)
    count = min(len(sound), len(dst)-start)
    if count > 0:
        dst[start:start+count] += sound[:count]*gain


def sequence(seconds, notes, instrument=bell, step=.08, gain=1):
    x = np.zeros(round(seconds*SR))
    for i, f in enumerate(notes):
        if f > 0:
            add(x, instrument(f, max(.15, seconds-i*step)), i*step, gain)
    return x


def write(name, x, music=False, loop=False):
    if not loop:
        n = min(round(.004*SR), len(x)//2)
        x[:n] *= np.linspace(0, 1, n)
        x[-n:] *= np.linspace(1, 0, n)
    else:
        # Retain wrapped decay while matching value and slope at the PCM seam.
        n = round(.004*SR)
        u = np.linspace(0, 1, n)
        ease = u*u*(3-2*u)
        seam = (x[0]+x[-1])*.5
        x[:n] = seam*(1-ease)+x[:n]*ease
        x[-n:] = x[-n:]*(1-ease)+seam*ease
    x *= 10**((-6 if music else -1)/20) / max(np.max(np.abs(x)), 1e-9)
    with wave.open(str(OUT / (name+'.wav')), 'wb') as w:
        w.setparams((1, 2, SR, 0, 'NONE', 'not compressed'))
        w.writeframes(np.rint(x*32767).astype('<i2').tobytes())
    print(f'{name}.wav: {len(x)/SR:.6f} s')


def sfx():
    sounds = {
        'click': mallet(720, .06),
        'pop': sweep(900, 210, .12)+noise(.12)*np.exp(-np.arange(5292)/450)*.1,
        'whoosh': air(.25),
        'land': mallet(680, .12)*.5+sweep(150, 85, .12),
        'merge': sequence(.5, [1046.5, 1318.5, 1568], step=.075),
        'reveal': sweep(300, 800, .3)*.5+sequence(.3, [0, 1568], step=.12),
        'empty': noise(.25, 300, 2800)*np.exp(-np.arange(11025)/1800)*.4+sweep(500, 160, .25),
        'cook_start': sequence(.4, [1500, 2100], step=.045)*.45+noise(.4)*np.linspace(0, .3, 17640),
        'ding': bell(1568, .9),
        'serve': sequence(.4, [523.25, 783.99], pluck, .13),
        'coin': sequence(.25, [1318.51, 1760], step=.075),
        'cheer': sequence(.6, [523.25, 659.25, 783.99, 1046.5], mallet, .105),
        'customer_in': sequence(.6, [1318.51, 1046.5, 1568], step=.115),
        'error': sequence(.25, [260, 220], mallet, .12),
        'tick': mallet(1150, .08),
        'hint': sequence(.5, [1568, 2093, 2637], step=.09),
        'button': sweep(500, 750, .12)+pluck(660, .12)*.35,
        'popup': air(.35, True)*.3+sequence(.35, [0, 880], pluck, .12)*.6,
        'trash': noise(.4, 400, 2600)*np.exp(-np.arange(17640)/5000)*.3+sequence(.4, [420, 330, 260], mallet, .08),
        'lose': sequence(1.8, [659.25, 587.33, 523.25, 392], sad_pluck, .35),
    }
    for i in range(1, 6):
        sounds[f'combo{i}'] = bell(1046.5*2**((i-1)*2/12), .35)
    for i in range(1, 4):
        sounds[f'star{i}'] = bell(784*2**((i-1)*3/12), .45)+mallet(100+i*30, .45)*.45
    win = sequence(2.5, [523.25, 659.25, 783.99, 1046.5, 1318.51, 1568], step=.16)
    for f in [261.63, 329.63, 392, 523.25]:
        t = np.arange(len(win))/SR
        win += .13*np.sin(2*np.pi*f*t)*adsr(len(win), .25, .3, .6, .6)
    win += noise(2.5, 3500, 9000)*np.exp(-t*2)*.15
    sounds['win'] = win
    for name, sound in sounds.items():
        write(name, sound)
    # Periodic FFT noise and periodic modulation give a continuous loop; overlap
    # the ends with equal-power weights to soften residual sample discontinuity.
    x = noise(2, 450, 6200)
    t = np.arange(len(x))/SR
    x *= .55+.12*np.sin(2*np.pi*3*t)+.08*np.sin(2*np.pi*11*t)
    blend = 2205
    bridge = (x[:blend]+x[-blend:])*.5
    w = np.linspace(0, 1, blend)
    x[:blend] = bridge*(1-w)+x[:blend]*w
    x[-blend:] = x[-blend:]*(1-w)+bridge*w
    # Tiny cyclic smoothing makes the final/first sample transition gentle.
    seam = (x[0]+x[-1])*.5
    x[:176] = seam*(1-np.linspace(0, 1, 176))+x[:176]*np.linspace(0, 1, 176)
    x[-176:] = x[-176:]*(1-np.linspace(0, 1, 176))+seam*np.linspace(0, 1, 176)
    write('sizzle', x, loop=True)


def music(name, bpm, game):
    beat = 60/bpm
    length = round(64*beat*SR)
    x = np.zeros(length+SR*4)
    roots = [130.8128, 110, 87.3071, 97.9989]
    scale = np.array([523.251, 587.33, 659.255, 783.991, 880, 1046.502])
    motifs = [[0, 2, 3, 2, 1, 0, 2, 4], [3, 4, 5, 4, 2, 3, 1, 0],
              [2, 0, 1, 2, 4, 3, 2, 0], [3, 2, 1, 0, 1, 2, 3, 0]]
    for bar in range(16):
        root = roots[(bar//2)%4]
        origin = bar*4*beat
        for ratio in [1, 1.5, 2.5]:
            t = np.arange(round(5*beat*SR))/SR
            pad = np.sin(2*np.pi*root*ratio*t)*adsr(len(t), .3, .4, .65, .8)
            add(x, pad, origin, .035)
        for j in range(4):
            t = np.arange(round(.7*beat*SR))/SR
            f = root*([1, 1.25, 1.5, 1.75][j] if game else 1)
            bass = (np.sin(2*np.pi*f*t)+.12*np.sin(6*np.pi*f*t))*adsr(len(t), .012, .12, .3, .09)
            add(x, bass, origin+j*beat, .19)
            add(x, sweep(140, 48, .14), origin+j*beat, .11 if game else .045)
            if j%2:
                drum = noise(.12, 700, 5000)*np.exp(-np.arange(5292)/1000)
                add(x, drum, origin+j*beat, .065 if game else .035)
        for j, index in enumerate(motifs[bar%4]):
            if not game and j in (3, 7):
                continue
            inst = pluck if game and (bar%2) else mallet
            add(x, inst(scale[index], .65), origin+j*.5*beat, .18)
            hat = noise(.055, 4000, 9500)*np.exp(-np.arange(round(.055*SR))/600)
            add(x, hat, origin+j*.5*beat, .025)
        if bar%4 == 3:
            add(x, bell(scale[5]*2, 1.2), origin+3*beat, .07)
    # Quiet delay, then wrap all tails into the first bar (periodic steady state).
    delay = round(beat*.75*SR)
    dry = x.copy()
    for i, gain in [(1, .16), (2, .065)]:
        x[i*delay:] += dry[:-i*delay]*gain
    loop = x[:length].copy()
    for start in range(length, len(x), length):
        tail = x[start:start+length]
        loop[:len(tail)] += tail
    write(name, loop, music=True, loop=True)


if __name__ == '__main__':
    OUT.mkdir(parents=True, exist_ok=True)
    sfx()
    music('bgm_home', 98, False)
    music('bgm_game', 112, True)

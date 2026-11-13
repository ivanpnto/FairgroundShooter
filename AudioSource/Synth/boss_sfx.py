# Síntesis de los efectos de sonido del combate contra el boss (bloque 2).
# Todo se genera con numpy/scipy: osciladores, ruido filtrado y envolventes. 44,1 kHz, mono, 16 bits.
import sys, numpy as np
from scipy.signal import butter, sosfilt
from scipy.io import wavfile

SR = 44100
rng = np.random.default_rng(7)
out = sys.argv[1] if len(sys.argv) > 1 else "../../Assets/Audio/Boss"   # uso: python boss_sfx.py [carpeta]

def t_axis(d): return np.arange(int(SR * d)) / SR
def env_ad(n, a, d_curve=4.0):
    e = np.ones(n); na = max(1, int(SR * a)); e[:na] = np.linspace(0, 1, na)
    tail = np.linspace(0, 1, n - na); e[na:] = np.exp(-d_curve * tail); return e
def bp(x, lo, hi, order=2): return sosfilt(butter(order, [lo, hi], 'bandpass', fs=SR, output='sos'), x)
def lp(x, f, order=2): return sosfilt(butter(order, f, 'lowpass', fs=SR, output='sos'), x)
def hp(x, f, order=2): return sosfilt(butter(order, f, 'highpass', fs=SR, output='sos'), x)
def osc(freq, kind='sine'):
    ph = 2 * np.pi * np.cumsum(freq) / SR
    if kind == 'sine': return np.sin(ph)
    if kind == 'saw': return 2 * ((ph / (2 * np.pi)) % 1) - 1
    if kind == 'square': return np.sign(np.sin(ph))
def sweep_bp(x, f0, f1, width=0.5, blocks=64):
    y = np.zeros_like(x); n = len(x); step = n // blocks + 1
    for b in range(blocks):
        s, e = b * step, min(n, (b + 1) * step)
        if s >= e: break
        f = f0 * (f1 / f0) ** (b / (blocks - 1))
        seg = bp(x[max(0, s - 2000):e], f * (1 - width / 2), f * (1 + width / 2))
        y[s:e] = seg[-(e - s):]
    return y
def fade(x, fi=0.005, fo=0.03):
    n = len(x); a, b = int(SR * fi), int(SR * fo)
    x[:a] *= np.linspace(0, 1, a); x[-b:] *= np.linspace(1, 0, b); return x
def save(name, x, peak=0.89):
    x = fade(np.asarray(x, dtype=np.float64))
    x = np.tanh(x / np.max(np.abs(x)) * 1.2); x = x / np.max(np.abs(x)) * peak
    wavfile.write(f"{out}/{name}.wav", SR, (x * 32767).astype(np.int16)); print(name, round(len(x) / SR, 2), "s")

# 1. Toma de aire del bocinazo (1,2 s): fuelle mecánico que se carga. Ruido de aire cuyo filtro sube,
#    chasquidos de trinquete que se aceleran y un motor que gime hacia arriba. Corta en seco al final.
d = 1.2; t = t_axis(d); n = len(t)
air = sweep_bp(rng.standard_normal(n), 250, 2200, 0.6) * np.linspace(0.2, 1.0, n) ** 1.5
motor = osc(180 + 320 * (t / d) ** 1.6, 'saw'); motor = lp(motor, 1400) * 0.18 * np.linspace(0.3, 1, n)
clicks = np.zeros(n); tc = 0.0
while tc < d - 0.03:
    i = int(tc * SR); k = np.arange(min(400, n - i))
    clicks[i:i + len(k)] += hp(rng.standard_normal(len(k)), 2500) * np.exp(-k / 40) * 0.9
    tc += 0.16 - 0.11 * (tc / d)
save("Boss_Inhale", air * 1.0 + motor + clicks * 0.5)

# 2. Bocinazo (0,9 s): bocina de payaso gigante. Diente de sierra con formantes nasales y caída de tono.
d = 0.9; t = t_axis(d); n = len(t)
f = 155 * (1 - 0.12 * (t / d) ** 2) * (1 + 0.01 * np.sin(2 * np.pi * 7 * t))
src = osc(f, 'saw') + 0.6 * osc(f * 2.01, 'square')
honk = bp(src, 500, 1300) * 1.4 + bp(src, 2000, 3200) * 0.5 + lp(src, 300) * 0.8
save("Boss_Honk", honk * env_ad(n, 0.02, 1.6) ** 0.6)

# 3. Aturdido (0,9 s): «boing» que cae con vibrato + golpe metálico inarmónico.
d = 0.9; t = t_axis(d); n = len(t)
fb = 420 * np.exp(-1.2 * t) * (1 + 0.08 * np.sin(2 * np.pi * 9 * t))
boing = osc(fb) * env_ad(n, 0.005, 3.0)
clang = sum(np.sin(2 * np.pi * 330 * p * t) * np.exp(-t * (6 + 3 * i)) / (i + 1) for i, p in enumerate([1, 2.76, 5.40, 8.93]))
save("Boss_Stun", boing + clang * 0.45)

# 4. Aviso del esbirro (0,6 s): muelle que tiembla (trémolo rápido) y sube de tono, con trinquete.
d = 0.6; t = t_axis(d); n = len(t)
spring = osc(520 + 520 * (t / d) ** 1.3, 'square'); spring = bp(spring, 400, 2500) * (0.55 + 0.45 * np.sin(2 * np.pi * 22 * t))
rat = hp(rng.standard_normal(n), 3000) * (np.sin(2 * np.pi * 30 * t) > 0.85) * 0.6
save("Minion_Warning", (spring + rat) * np.linspace(0.6, 1.0, n))

# 5. Golpe recibido (0,5 s): bombo grave que cae + crujido.
d = 0.5; t = t_axis(d); n = len(t)
thump = osc(95 * np.exp(-6 * t) + 38) * env_ad(n, 0.002, 7)
crunch = lp(rng.standard_normal(n), 2500) * env_ad(n, 0.001, 14) * 0.7
save("Player_Hit", thump * 1.2 + crunch)

# 6. Tartazo (0,6 s): plaf húmedo (ruido grave con ataque rápido) y chapoteos pequeños.
d = 0.6; t = t_axis(d); n = len(t)
splat = lp(rng.standard_normal(n), 1100) * env_ad(n, 0.003, 9)
plop = osc(160 * np.exp(-8 * t) + 60) * env_ad(n, 0.002, 10) * 0.8
squish = np.zeros(n)
for s in (0.12, 0.2, 0.31):
    i = int(s * SR); k = np.arange(int(0.07 * SR))
    squish[i:i + len(k)] += bp(rng.standard_normal(len(k)), 600, 1800) * np.sin(np.pi * k / len(k)) * 0.35
save("Pie_Splat", splat + plop + squish)

# 7. Corazón recuperado (0,7 s): arpegio de campanillas Do-Mi-Sol-Do.
d = 0.7; t = t_axis(d); n = len(t); chime = np.zeros(n)
for j, fr in enumerate([1046.5, 1318.5, 1568.0, 2093.0]):
    i = int(j * 0.07 * SR); tt = t[: n - i]
    chime[i:] += (np.sin(2 * np.pi * fr * tt) + 0.3 * np.sin(2 * np.pi * fr * 2.01 * tt)) * np.exp(-tt * 5)
save("Heart_Gain", chime, peak=0.7)

# 8. Cambio de fase (1 s): chatarra que cae: golpes metálicos inarmónicos y ruido.
d = 1.0; t = t_axis(d); n = len(t); crash = np.zeros(n)
for j, (s, f0) in enumerate([(0.0, 180), (0.12, 260), (0.27, 140)]):
    i = int(s * SR); tt = t[: n - i]
    part = sum(np.sin(2 * np.pi * f0 * p * tt) * np.exp(-tt * (5 + 4 * k)) / (k + 1) for k, p in enumerate([1, 2.4, 3.9, 6.3, 8.7]))
    crash[i:] += part + hp(rng.standard_normal(len(tt)), 1500) * np.exp(-tt * 18) * 0.6
save("Boss_PhaseCrash", crash)

# 9. Fanfarria de victoria (~2,6 s): metales (sierra filtrada con envolvente de brillo) Sol-Do-Mi-Sol y Do largo.
notes = [(392.0, 0.0, 0.16), (523.3, 0.16, 0.16), (659.3, 0.32, 0.16), (784.0, 0.48, 0.42), (659.3, 0.95, 0.15), (1046.5, 1.12, 1.4)]
d = 2.6; t = t_axis(d); n = len(t); fan = np.zeros(n)
for fr, s, dur in notes:
    i = int(s * SR); m = int(dur * SR); tt = t[:m]
    vib = 1 + (0.006 * np.sin(2 * np.pi * 5.5 * tt) if dur > 1 else 0)
    tone = osc(fr * vib * np.ones(m), 'saw') + 0.5 * osc(fr * 1.003 * vib * np.ones(m), 'saw')
    bright = lp(tone, 1200 + 2600 * np.exp(-tt * 3).mean())
    e = np.minimum(1, tt / 0.02) * np.where(tt > dur - 0.05, (dur - tt) / 0.05, 1)
    fan[i:i + m] += bright * e
drum = np.zeros(n)
for s in (0.0, 0.48, 1.12):
    i = int(s * SR); k = t[: int(0.25 * SR)]
    drum[i:i + len(k)] += osc(110 * np.exp(-10 * k) + 50) * np.exp(-k * 14) + lp(rng.standard_normal(len(k)), 3000) * np.exp(-k * 25) * 0.4
save("Result_Victory", fan + drum * 0.9)

# 10. Derrota (~2,9 s): trombón triste «wah wah wah waaah» (semitonos descendentes con filtro wah).
d = 2.9; t = t_axis(d); n = len(t); trb = np.zeros(n)
for fr, s, dur in [(293.7, 0.0, 0.42), (277.2, 0.48, 0.42), (261.6, 0.96, 0.42), (246.9, 1.44, 1.4)]:
    i = int(s * SR); m = int(dur * SR); tt = t[:m]
    vib = 1 + (0.02 * np.sin(2 * np.pi * 6 * tt) * np.minimum(1, tt / 0.4) if dur > 1 else 0)
    tone = osc(fr * vib * np.ones(m), 'saw')
    wah = sweep_bp(tone, 350, 1400 if dur < 1 else 900, 0.9, blocks=24)
    e = np.minimum(1, tt / 0.03) * np.where(tt > dur - 0.08, np.maximum(0, (dur - tt) / 0.08), 1)
    trb[i:i + m] += (wah * 1.5 + lp(tone, 500) * 0.5) * e
save("Result_Defeat", trb)

# Síntesis de los efectos de la botica (Fase 8): el tónico que cae en el frasco, el corcho y el billete dorado.
# Mismo método que armory_sfx.py: numpy/scipy, 44,1 kHz, mono, 16 bits. La caja registradora y el paso del
# ratón se reutilizan de la armería.
import sys, numpy as np
from scipy.signal import butter, sosfilt
from scipy.io import wavfile

SR = 44100
rng = np.random.default_rng(41)
out = sys.argv[1] if len(sys.argv) > 1 else "../../Assets/Audio/Botica"   # uso: python botica_sfx.py [carpeta]

def t_axis(d): return np.arange(int(SR * d)) / SR
def bp(x, lo, hi, order=2): return sosfilt(butter(order, [lo, hi], 'bandpass', fs=SR, output='sos'), x)
def lp(x, f, order=2): return sosfilt(butter(order, f, 'lowpass', fs=SR, output='sos'), x)
def hp(x, f, order=2): return sosfilt(butter(order, f, 'highpass', fs=SR, output='sos'), x)
def fade(x, fi=0.003, fo=0.03):
    a, b = int(SR * fi), int(SR * fo)
    x[:a] *= np.linspace(0, 1, a); x[-b:] *= np.linspace(1, 0, b); return x
def save(name, x, peak=0.89):
    x = fade(np.asarray(x, dtype=np.float64))
    x = np.tanh(x / np.max(np.abs(x)) * 1.2); x = x / np.max(np.abs(x)) * peak
    wavfile.write(f"{out}/{name}.wav", SR, (x * 32767).astype(np.int16)); print(name, round(len(x) / SR, 2), "s")
def place(buf, sig, at):
    i = int(at * SR); n = min(len(sig), len(buf) - i); buf[i:i + n] += sig[:n]

# 1. Tónico que cae en el frasco (0,75 s): el chorro (ruido filtrado) y una serie de «glu» cada vez más
#    seguidos y más agudos, porque al llenarse queda menos aire en el frasco. Cada «glu» es una burbuja:
#    un seno que sube de tono deprisa y se apaga.
d = 0.75; buf = np.zeros(int(SR * d)); t = t_axis(d)
stream = bp(rng.standard_normal(len(t)), 500, 2600) * 0.18
stream *= np.clip(t / 0.04, 0, 1) * np.clip((d - t) / 0.12, 0, 1)
buf += stream
at = 0.02
for k in range(7):
    dur = 0.07
    tt = t_axis(dur)
    f0 = 260 + 70 * k + rng.uniform(-20, 20)
    freq = f0 * (1 + 1.4 * tt / dur)                       # la burbuja sube de tono al salir
    phase = 2 * np.pi * np.cumsum(freq) / SR
    glug = np.sin(phase) * np.exp(-tt * 38) * (0.9 - 0.06 * k)
    place(buf, glug, at)
    at += 0.11 - 0.008 * k
save("Tonic_Glug", buf, peak=0.8)

# 2. Corcho (0,35 s): «pop» (barrido rápido de grave a más grave con un chasquido) y un siseo de burbujas
d = 0.35; buf = np.zeros(int(SR * d)); t = t_axis(d)
tt = t_axis(0.06)
freq = 950 * np.exp(-tt * 30) + 280
pop = np.sin(2 * np.pi * np.cumsum(freq) / SR) * np.exp(-tt * 45)
click = hp(rng.standard_normal(len(tt)), 2500) * np.exp(-tt * 160) * 0.6
place(buf, pop + click, 0.0)
fizz = hp(rng.standard_normal(len(t)), 5000) * np.exp(-t * 9) * 0.12 * np.clip((t - 0.03) / 0.02, 0, 1)
buf += fizz
save("Cork_Pop", buf, peak=0.8)

# 3. Billete dorado (1,1 s): arpegio de campanillas que sube (do-mi-sol-do) con un brillo de fondo.
d = 1.1; buf = np.zeros(int(SR * d)); t = t_axis(d)
def chime(f, dur, amp):
    tt = t_axis(dur)
    tone = np.sin(2 * np.pi * f * tt) + 0.35 * np.sin(2 * np.pi * f * 2.0 * tt) + 0.15 * np.sin(2 * np.pi * f * 3.01 * tt)
    return tone * np.exp(-tt * 4.5) * amp
for k, f in enumerate([1047, 1319, 1568, 2093]):
    place(buf, chime(f, 0.8, 0.5 + 0.1 * k), 0.06 * k)
shimmer = bp(rng.standard_normal(len(t)), 6000, 12000) * 0.08
shimmer *= np.clip(t / 0.15, 0, 1) * np.exp(-t * 3) * (0.6 + 0.4 * np.sin(2 * np.pi * 13 * t))
buf += shimmer
save("Ticket_Shine", buf, peak=0.75)

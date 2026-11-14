# Síntesis de los efectos de la armería (Fase 8): caja registradora, bombilla y paso del ratón por un arma.
# Mismo método que boss_sfx.py y transition_sfx.py: numpy/scipy, 44,1 kHz, mono, 16 bits.
import sys, numpy as np
from scipy.signal import butter, sosfilt
from scipy.io import wavfile

SR = 44100
rng = np.random.default_rng(23)
out = sys.argv[1] if len(sys.argv) > 1 else "../../Assets/Audio/Armory"   # uso: python armory_sfx.py [carpeta]

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

# 1. Caja registradora (1,1 s): «cha-ching». Golpe mecánico de la tecla, el cajón que sale rodando y la
#    campanilla de dos notas (la segunda más aguda y larga), con unas monedas que tintinean al final.
d = 1.1; buf = np.zeros(int(SR * d)); t = t_axis(d)
clack = bp(rng.standard_normal(int(0.05 * SR)), 1200, 5000) * np.exp(-t_axis(0.05) * 90)
place(buf, clack * 0.9, 0.0)
roll = bp(rng.standard_normal(int(0.25 * SR)), 300, 1600) * np.sin(np.pi * np.linspace(0, 1, int(0.25 * SR))) * 0.35
place(buf, roll, 0.04)
def bell(f, dur, amp):
    tt = t_axis(dur)
    tone = (np.sin(2 * np.pi * f * tt) + 0.5 * np.sin(2 * np.pi * f * 2.76 * tt) + 0.25 * np.sin(2 * np.pi * f * 5.4 * tt))
    return tone * np.exp(-tt * 5.5) * amp
place(buf, bell(1568, 0.6, 0.55), 0.12)    # «cha»
place(buf, bell(2093, 0.9, 0.8), 0.24)     # «ching»
for k in range(5):
    tt = t_axis(0.08); f = rng.uniform(3500, 6000)
    place(buf, np.sin(2 * np.pi * f * tt) * np.exp(-tt * 60) * 0.25, 0.45 + k * 0.06 + rng.uniform(0, 0.03))
save("Register_ChaChing", buf)

# 2. Bombilla que se enciende (0,45 s): chasquido del interruptor y un zumbido eléctrico que parpadea y se asienta.
d = 0.45; t = t_axis(d); n = len(t)
click = hp(rng.standard_normal(n), 3000) * np.exp(-t * 120) * 0.8
flick = np.where(((t > 0.05) & (t < 0.09)) | (t > 0.13), 1.0, 0.15)
hum = (np.sin(2 * np.pi * 100 * t) * 0.5 + np.sin(2 * np.pi * 200 * t) * 0.3 + bp(rng.standard_normal(n), 2000, 6000) * 0.15)
hum *= flick * np.exp(-np.maximum(0, t - 0.15) * 6) * np.clip(t / 0.05, 0, 1)
save("Bulb_On", click + hum * 0.6, peak=0.7)

# 3. Paso del ratón por un arma (0,12 s): golpecito metálico suave, como rozar el soporte.
d = 0.12; t = t_axis(d)
tick = (np.sin(2 * np.pi * 2600 * t) + 0.4 * np.sin(2 * np.pi * 4100 * t)) * np.exp(-t * 55)
save("Armory_Hover", tick, peak=0.45)

# Síntesis de los sonidos de las mecánicas de los niveles, con el mismo estilo que boss_sfx.py:
# numpy/scipy, 44,1 kHz, mono, 16 bits, pico a −1 dB.
#   uso: python level_sfx.py [carpeta]   (por defecto ../../Assets/Audio/Levels)
import os, sys, numpy as np
from scipy.signal import butter, sosfilt
from scipy.io import wavfile

SR = 44100
rng = np.random.default_rng(23)
out = sys.argv[1] if len(sys.argv) > 1 else "../../Assets/Audio/Levels"
os.makedirs(out, exist_ok=True)

def t_axis(d): return np.arange(int(SR * d)) / SR
def bp(x, lo, hi, order=2): return sosfilt(butter(order, [lo, hi], 'bandpass', fs=SR, output='sos'), x)
def fade(x, fi=0.005, fo=0.03):
    a, b = int(SR * fi), int(SR * fo)
    x[:a] *= np.linspace(0, 1, a); x[-b:] *= np.linspace(1, 0, b); return x
def save(name, x, peak=0.89):
    x = fade(np.asarray(x, dtype=np.float64))
    x = np.tanh(x / np.max(np.abs(x)) * 1.2); x = x / np.max(np.abs(x)) * peak
    wavfile.write(f"{out}/{name}.wav", SR, (x * 32767).astype(np.int16)); print(name, round(len(x) / SR, 2), "s")

def sweep_bp(x, freqs, width=0.6, blocks=96):
    # Ruido filtrado con un pasabanda que sigue la curva freqs (un valor por muestra), por bloques
    y = np.zeros_like(x); n = len(x); step = n // blocks + 1
    for b in range(blocks):
        s, e = b * step, min(n, (b + 1) * step)
        if s >= e: break
        f = float(np.mean(freqs[s:e]))
        seg = bp(x[max(0, s - 2000):e], f * (1 - width / 2), f * (1 + width / 2))
        y[s:e] = seg[-(e - s):]
    return y

# Ráfaga de viento (3,2 s = WindGusts.warningTime 1,2 + gustTime 2): sube durante el aviso, llega al máximo en
# mitad de la ráfaga y se va. Dos capas de ruido: un soplido grave que sube y baja de tono y un silbido más agudo
t = t_axis(3.2)
peak_at = 1.2 + 1.0
env = np.where(t < peak_at, (t / peak_at) ** 1.8, np.exp(-2.2 * (t - peak_at)))
f_low = 260 + 520 * env
f_high = 900 + 1300 * env
noise = rng.standard_normal(len(t))
gust = sweep_bp(noise, f_low, width=0.8) * 1.0 + sweep_bp(rng.standard_normal(len(t)), f_high, width=0.35) * 0.35
flutter = 1 + 0.15 * np.sin(2 * np.pi * 3.3 * t) * env       # el viento no sopla parejo
save("Wind_Gust", gust * env * flutter)

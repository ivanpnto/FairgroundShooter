# Síntesis de los sonidos del rayo del boss (BossAttack.LaserSweep), con el mismo estilo que boss_sfx.py:
# numpy/scipy, 44,1 kHz, mono, 16 bits, pico a −1 dB.
#   uso: python laser_sfx.py [carpeta]   (por defecto ../../Assets/Audio/Boss)
import sys, numpy as np
from scipy.signal import butter, sosfilt
from scipy.io import wavfile

SR = 44100
rng = np.random.default_rng(11)
out = sys.argv[1] if len(sys.argv) > 1 else "../../Assets/Audio/Boss"

def t_axis(d): return np.arange(int(SR * d)) / SR
def lp(x, f, order=2): return sosfilt(butter(order, f, 'lowpass', fs=SR, output='sos'), x)
def hp(x, f, order=2): return sosfilt(butter(order, f, 'highpass', fs=SR, output='sos'), x)
def osc(freq, kind='sine'):
    ph = 2 * np.pi * np.cumsum(freq) / SR
    if kind == 'sine': return np.sin(ph)
    if kind == 'saw': return 2 * ((ph / (2 * np.pi)) % 1) - 1
def fade(x, fi=0.005, fo=0.03):
    a, b = int(SR * fi), int(SR * fo)
    x[:a] *= np.linspace(0, 1, a); x[-b:] *= np.linspace(1, 0, b); return x
def save(name, x, peak=0.89):
    x = fade(np.asarray(x, dtype=np.float64))
    x = np.tanh(x / np.max(np.abs(x)) * 1.2); x = x / np.max(np.abs(x)) * peak
    wavfile.write(f"{out}/{name}.wav", SR, (x * 32767).astype(np.int16)); print(name, round(len(x) / SR, 2), "s")

# 1. Carga (1,5 s, lo que dura laserTelegraphTime): un zumbido que sube de tono y se acelera, para que se oiga
#    cuánto falta sin mirar. Dos sierras desafinadas (más «eléctrico» que un seno) con un trémolo cada vez más rápido
t = t_axis(1.5); k = t / t[-1]
f = 180 * (1200 / 180) ** (k ** 1.6)                   # sube despacio y al final de golpe
tone = osc(f, 'saw') + 0.6 * osc(f * 1.007, 'saw') + 0.3 * osc(f * 2, 'sine')
trem = 0.65 + 0.35 * np.sin(2 * np.pi * np.cumsum(4 + 26 * k ** 2) / SR)
hiss = hp(rng.standard_normal(len(t)), 3000) * 0.15 * k
charge = lp(tone, 5000) * trem * (0.25 + 0.75 * k ** 1.2) + hiss
save("Laser_Charge", charge)

# 2. Disparo y barrido (1,1 s, laserSweepTime + cola): golpe inicial y un zumbido grave y sucio que se mantiene
#    mientras el rayo cruza la caseta, con un silbido agudo encima; se apaga al final
t = t_axis(1.1); k = t / t[-1]
body = osc(np.full(len(t), 92.0), 'saw') + 0.7 * osc(np.full(len(t), 92.0 * 1.5), 'saw')
wobble = 1 + 0.08 * np.sin(2 * np.pi * 7 * t)
whine = osc(np.full(len(t), 1650.0) * wobble, 'sine') * 0.35
grit = lp(rng.standard_normal(len(t)), 1800) * 0.5
env = np.minimum(1, t / 0.01) * (1 - k ** 3)
hit = np.exp(-t / 0.05) * lp(rng.standard_normal(len(t)), 900) * 1.5   # el chasquido del principio
beam = (lp(body * wobble, 2500) + whine + grit) * env + hit
save("Laser_Beam", beam)

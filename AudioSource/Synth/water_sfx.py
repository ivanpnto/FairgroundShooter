# Síntesis de los sonidos del chorro de agua del boss (BossAttack.LaserSweep: la flor del ojal), con el mismo
# estilo que boss_sfx.py y laser_sfx.py: numpy/scipy, 44,1 kHz, mono, 16 bits, pico a −1 dB.
# Sustituye a laser_sfx.py (24/11/2026: el rayo por los ojos se cambió por la flor que echa agua).
#   uso: python water_sfx.py [carpeta]   (por defecto ../../Assets/Audio/Boss)
import sys, numpy as np
from scipy.signal import butter, sosfilt
from scipy.io import wavfile

SR = 44100
rng = np.random.default_rng(24)
out = sys.argv[1] if len(sys.argv) > 1 else "../../Assets/Audio/Boss"

def t_axis(d): return np.arange(int(SR * d)) / SR
def lp(x, f, order=2): return sosfilt(butter(order, f, 'lowpass', fs=SR, output='sos'), x)
def hp(x, f, order=2): return sosfilt(butter(order, f, 'highpass', fs=SR, output='sos'), x)
def bp(x, lo, hi, order=2): return sosfilt(butter(order, [lo, hi], 'bandpass', fs=SR, output='sos'), x)
def osc(freq):
    return np.sin(2 * np.pi * np.cumsum(freq) / SR)
def fade(x, fi=0.005, fo=0.04):
    a, b = int(SR * fi), int(SR * fo)
    x[:a] *= np.linspace(0, 1, a); x[-b:] *= np.linspace(1, 0, b); return x
def save(name, x, peak=0.89):
    x = fade(np.asarray(x, dtype=np.float64))
    x = np.tanh(x / np.max(np.abs(x)) * 1.2); x = x / np.max(np.abs(x)) * peak
    wavfile.write(f"{out}/{name}.wav", SR, (x * 32767).astype(np.int16)); print(name, round(len(x) / SR, 2), "s")

def bubbles(n_samples, rate, f_lo, f_hi, decay=0.03):
    # Burbujas: pitidos cortos que suben de tono (así suena una burbuja que se forma), a intervalos al azar
    y = np.zeros(n_samples)
    t = 0
    while True:
        t += int(SR / rate * rng.uniform(0.3, 1.7))
        if t >= n_samples: break
        d = int(SR * decay * rng.uniform(0.6, 1.6))
        tt = np.arange(min(d, n_samples - t)) / SR
        f0 = rng.uniform(f_lo, f_hi)
        f = f0 * (1 + 2.5 * tt / (decay * 2))
        y[t:t + len(tt)] += np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-tt / (decay * 0.4)) * rng.uniform(0.4, 1)
    return y

# 1. Carga (1,5 s, lo que dura laserTelegraphTime): la flor se llena de agua. Bombeo de pera de goma (apretones
#    cada vez más seguidos, con su chirrido) y burbujeo que va subiendo: se oye cuánto falta sin mirar
t = t_axis(1.5); k = t / t[-1]
squeeze_rate = 3 + 9 * k ** 1.5                     # apretones por segundo, cada vez más rápidos
pump_phase = np.cumsum(squeeze_rate) / SR
pump_env = np.clip(np.sin(2 * np.pi * pump_phase), 0, None) ** 3
squeak = osc(700 + 500 * k + 120 * np.sin(2 * np.pi * pump_phase)) * pump_env * 0.35
squish = lp(rng.standard_normal(len(t)), 900) * pump_env * 0.9
bub = bubbles(len(t), 18 + 40 * 1.0, 300, 900) * (0.3 + 0.7 * k)
pressure = bp(rng.standard_normal(len(t)), 2500, 7000) * 0.25 * k ** 3    # siseo de presión al final
charge = squeak + squish + bub + pressure
save("Water_Charge", charge)

# 2. Chorro (1,1 s, laserSweepTime + cola): chorro a presión. Ruido de banda ancha con turbulencia (la presión
#    sube y baja un poco), el golpe de agua del principio y un salpicar continuo por debajo; se corta al final
t = t_axis(1.1); k = t / t[-1]
turb = 1 + 0.25 * lp(rng.standard_normal(len(t)), 12) * 4
jet = bp(rng.standard_normal(len(t)), 1200, 9000) * turb
low = lp(rng.standard_normal(len(t)), 500) * 0.6
splat = bubbles(len(t), 60, 600, 2200, decay=0.015) * 0.5
env = np.minimum(1, t / 0.02) * (1 - k ** 4)
burst = np.exp(-t / 0.06) * lp(rng.standard_normal(len(t)), 1500) * 1.8
spray = (jet + low + splat) * env + burst
save("Water_Spray", spray)

# 3. Te moja (0,7 s): chapuzón en la cara, para el efecto de pantalla mojada
t = t_axis(0.7); k = t / t[-1]
hit = lp(rng.standard_normal(len(t)), 2500) * np.exp(-t / 0.08) * 1.5
drip = bubbles(len(t), 25, 500, 1500, decay=0.02) * (1 - k) * 0.6
splash = hit + drip + bp(rng.standard_normal(len(t)), 3000, 8000) * np.exp(-t / 0.15) * 0.5
save("Water_Splash", splash)

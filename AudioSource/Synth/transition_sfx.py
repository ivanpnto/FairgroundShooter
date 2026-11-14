# Síntesis de los efectos de la transición entre niveles (Fase 8): telón, cartel y cuenta atrás.
# Mismo método que boss_sfx.py: numpy/scipy, 44,1 kHz, mono, 16 bits.
import sys, numpy as np
from scipy.signal import butter, sosfilt
from scipy.io import wavfile

SR = 44100
rng = np.random.default_rng(11)
out = sys.argv[1] if len(sys.argv) > 1 else "../../Assets/Audio/Transition"   # uso: python transition_sfx.py [carpeta]

def t_axis(d): return np.arange(int(SR * d)) / SR
def bp(x, lo, hi, order=2): return sosfilt(butter(order, [lo, hi], 'bandpass', fs=SR, output='sos'), x)
def lp(x, f, order=2): return sosfilt(butter(order, f, 'lowpass', fs=SR, output='sos'), x)
def hp(x, f, order=2): return sosfilt(butter(order, f, 'highpass', fs=SR, output='sos'), x)
def fade(x, fi=0.004, fo=0.03):
    a, b = int(SR * fi), int(SR * fo)
    x[:a] *= np.linspace(0, 1, a); x[-b:] *= np.linspace(1, 0, b); return x
def save(name, x, peak=0.89):
    x = fade(np.asarray(x, dtype=np.float64))
    x = np.tanh(x / np.max(np.abs(x)) * 1.2); x = x / np.max(np.abs(x)) * peak
    wavfile.write(f"{out}/{name}.wav", SR, (x * 32767).astype(np.int16)); print(name, round(len(x) / SR, 2), "s")

# 1. Telón (0,85 s): tela pesada que se arrastra. Ruido filtrado en banda media con una envolvente que
#    sube y baja (el roce) y un leve aleteo a 9 Hz (los pliegues). Vale para abrir y para cerrar.
d = 0.85; t = t_axis(d); n = len(t)
cloth = bp(rng.standard_normal(n), 300, 2600) * 0.8 + bp(rng.standard_normal(n), 2600, 6000) * 0.25
shape = np.sin(np.pi * np.clip(t / d, 0, 1)) ** 1.4
flutter = 1 + 0.25 * np.sin(2 * np.pi * 9 * t + 2 * np.sin(2 * np.pi * 1.3 * t))
save("Curtain_Swoosh", cloth * shape * flutter, peak=0.7)

# 2. Golpe tras el telón (0,35 s): algo pesado (un decorado de madera) que choca detrás de la tela.
#    Seno grave que cae de tono + chasquido de madera apagado (la tela filtra los agudos).
d = 0.35; t = t_axis(d); n = len(t)
f = 95 * (1 - 0.35 * t / d)
body = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 14)
knock = lp(rng.standard_normal(n), 900) * np.exp(-t * 60) * 0.9
save("Curtain_Thump", body + knock, peak=0.8)

# 3. Cartel (0,5 s): caja de feria. Golpe de bombo corto + platillo pequeño que se apaga, como el «¡tachán!»
#    de un número de feria pero breve: anuncia sin tapar la música.
d = 0.5; t = t_axis(d); n = len(t)
kick = np.sin(2 * np.pi * np.cumsum(120 * np.exp(-t * 18) + 55) / SR) * np.exp(-t * 16)
cymbal = hp(rng.standard_normal(n), 5000) * np.exp(-t * 9) * 0.35
snap = bp(rng.standard_normal(n), 1500, 4000) * np.exp(-t * 80) * 0.6
save("Card_Pop", kick + cymbal + snap)

# 4. Tic de la cuenta atrás (0,18 s): bloque de madera. Dos parciales inarmónicos con caída rápida.
d = 0.18; t = t_axis(d)
tick = (np.sin(2 * np.pi * 1180 * t) + 0.5 * np.sin(2 * np.pi * 2730 * t)) * np.exp(-t * 38)
save("Count_Tick", tick, peak=0.75)

# 5. ¡FUEGO! (0,55 s): el mismo bloque una quinta más agudo, con una campanilla que se queda sonando.
d = 0.55; t = t_axis(d)
go = (np.sin(2 * np.pi * 1770 * t) + 0.5 * np.sin(2 * np.pi * 4090 * t)) * np.exp(-t * 30)
bell = (np.sin(2 * np.pi * 2350 * t) + 0.4 * np.sin(2 * np.pi * 5640 * t)) * np.exp(-t * 6) * 0.45
save("Count_Go", go + bell, peak=0.8)

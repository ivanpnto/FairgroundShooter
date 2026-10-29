# AudioSource

Originales de Freesound (en `raw/`, sin modificar) y cómo se han procesado los clips de
`Assets/Audio/Weapons/`. Créditos y licencias en `CREDITS.md`, en la raíz del repo.

## Procesado común (Audacity)
1. Importar el original.
2. Borrar desde 0 hasta el inicio (el ataque del sonido, en el cruce por cero
   inmediatamente anterior) y desde inicio + duración hasta el final.
3. Stereo to Mono.
4. Fade Out lineal en los últimos N segundos.
5. Normalize a −1 dB (con eliminación de DC).
6. Exportar WAV 16 bits, 44100 Hz, mono.

Los originales a 48 kHz se remuestrean a 44,1 kHz al exportar.

## Clips
| Exportado | Original | Inicio (s) | Duración (s) | Fade out (s) | Original estéreo |
|---|---|---|---|---|---|
| FiveSeven_Shot.wav | fiveseven_raw.wav (44,1 kHz) | 0.00034 | 0.60 | 0.15 | Sí |
| MP5_Shot.wav | mp5_raw.aiff (48 kHz) | 1.71202 | 0.35 | 0.10 | Sí |
| AK47_Shot.wav | ak47_raw.aiff (48 kHz) | 2.86704 | 0.40 | 0.10 | Sí |
| M16A1_Shot.wav | m16a1_raw.flac (44,1 kHz) | 0.00075 | 0.50 | 0.12 | Sí |
| Barrett_Shot.wav | barrett_raw.wav (48 kHz) | 0.02656 | 2.00 | 0.60 | Sí |
| Rifle_MagOut.wav | rifle_reload_raw.wav (44,1 kHz) | 0.53163 | 0.67 | 0.05 | Sí |
| Rifle_MagIn.wav | rifle_reload_raw.wav (44,1 kHz) | 1.23175 | 1.10 | 0.05 | Sí |
| Pistol_MagOut.wav | pistol_reload_raw.mp3 (44,1 kHz) | 0.12608 | 0.30 | 0.05 | Sí |
| Pistol_MagIn.wav | pistol_reload_raw.mp3 (44,1 kHz) | 0.77803 | 1.06 | 0.05 | Sí |

## Notas
- **Ráfagas (MP5, AK47):** los disparos se suceden cada ~88 ms (MP5) y ~100 ms (AK47),
  así que cualquier disparo intermedio llevaría dentro los 3–4 siguientes. Se usa el
  **último disparo de la ráfaga**: detrás solo tiene su propia cola, y delante, la del
  anterior, que queda tapada por el ataque.
- **Barrett:** el original está muy saturado y, al remuestrear de 48 a 44,1 kHz, los picos
  superaban 0 dBFS (había muestras recortadas). Se hace en dos pasadas: procesado y
  normalizado a −3 dB → exportar a 44,1 kHz → reimportar ese WAV → Normalize a −1 dB →
  exportar.
- **pistol_reload_raw.mp3:** Audacity lo decodifica con la misma duración que ffmpeg
  (2.378 s); el silencio inicial (0–0.126 s) queda eliminado por el corte.
- **Recarga de rifle:** se descartan los golpes de 2.4–3.4 s (parecen la palanca de carga).
- **Recarga de pistola:** MagOut = el clic de 0.13–0.35 s; MagIn = el golpe de 0.78–1.24 s
  más la corredera de 1.50–1.84 s.
